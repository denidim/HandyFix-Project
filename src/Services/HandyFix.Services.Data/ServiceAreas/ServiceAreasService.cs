namespace HandyFix.Services.Data.ServiceAreas
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    using HandyFix.Data.Common.Repositories;
    using HandyFix.Data.Models;
    using HandyFix.Services.Mapping;
    using HandyFix.Web.ViewModels.ServiceAreas;
    using HandyFix.Web.ViewModels.Validation;

    using Microsoft.AspNetCore.Http;
    using Microsoft.EntityFrameworkCore;

    public class ServiceAreasService : IServiceAreasService
    {
        // The centre of the coverage diagram, the Chessington base: the middle of the KT9 district
        // as postcodes.io gives it. Fixed here rather than looked up, since the base does not move.
        private static readonly GeoPoint DiagramCentre = new GeoPoint(51.3638, -0.3033);

        private readonly IDeletableEntityRepository<ServiceArea> areasRepository;
        private readonly IDeletableEntityRepository<ServiceAreaFaq> faqsRepository;
        private readonly IImageStorageService imageStorageService;
        private readonly IPostcodeLocator postcodeLocator;
        private readonly IDriveTimeCalculator driveTimeCalculator;

        public ServiceAreasService(
            IDeletableEntityRepository<ServiceArea> areasRepository,
            IDeletableEntityRepository<ServiceAreaFaq> faqsRepository,
            IImageStorageService imageStorageService,
            IPostcodeLocator postcodeLocator,
            IDriveTimeCalculator driveTimeCalculator)
        {
            this.areasRepository = areasRepository;
            this.faqsRepository = faqsRepository;
            this.imageStorageService = imageStorageService;
            this.postcodeLocator = postcodeLocator;
            this.driveTimeCalculator = driveTimeCalculator;
        }

        public async Task<IEnumerable<T>> GetAllAsync<T>(bool featuredFirst = true)
        {
            // Nearest first, then by name (PROJECT_STATE.md Section 3dn): the admin no longer sets
            // an order by hand.
            IQueryable<ServiceArea> query = this.areasRepository.All();

            query = featuredFirst
                ? query.OrderByDescending(x => x.IsFeatured).ThenBy(x => x.DriveTimeMinutes).ThenBy(x => x.Name)
                : query.OrderBy(x => x.DriveTimeMinutes).ThenBy(x => x.Name);

            return await query.To<T>().ToListAsync();
        }

        public async Task<T> GetBySlugAsync<T>(string slug)
        {
            return await this.areasRepository.All()
                .Where(x => x.Slug == slug.ToLower())
                .To<T>()
                .FirstOrDefaultAsync();
        }

        public async Task<T> GetByIdAsync<T>(Guid id)
        {
            return await this.areasRepository.All()
                .Where(x => x.Id == id)
                .To<T>()
                .FirstOrDefaultAsync();
        }

        public async Task<IEnumerable<T>> GetNearestAsync<T>(Guid excludeAreaId, int take = 4)
        {
            var currentDriveTime = await this.areasRepository.All()
                .Where(x => x.Id == excludeAreaId)
                .Select(x => (int?)x.DriveTimeMinutes)
                .FirstOrDefaultAsync();

            if (currentDriveTime == null)
            {
                return Enumerable.Empty<T>();
            }

            // Two areas the same drive away go by name, so an area page shows the same neighbours
            // on every visit.
            return await this.areasRepository.All()
                .Where(x => x.Id != excludeAreaId)
                .OrderBy(x => Math.Abs(x.DriveTimeMinutes - currentDriveTime.Value))
                .ThenBy(x => x.Name)
                .Take(take)
                .To<T>()
                .ToListAsync();
        }

        public async Task<ServiceAreaSaveResult> CreateAsync(ServiceAreaAdminInputModel model)
        {
            string name = model.Name.Trim();

            // The page address is made from the name once, here, and never changes after.
            string slug = SlugFromName(name);
            if (slug.Length == 0)
            {
                return ServiceAreaSaveResult.Failed(nameof(model.Name), "Use letters in the name.");
            }

            // All rows, deleted ones too: the unique index on the slug covers them.
            if (await this.areasRepository.AllWithDeleted().AnyAsync(x => x.Slug == slug))
            {
                return ServiceAreaSaveResult.Failed(nameof(model.Name), $"There is already an area called {name}.");
            }

            string districts = await this.DistrictsOrTownsAsync(model.PostcodeDistricts, name);
            if (districts == null)
            {
                return TownNotFound(name);
            }

            // A new area needs its place: the diagram and the drive time both come from it.
            string firstDistrict = UkPostcode.ParseDistricts(districts)[0];
            GeoPoint? place = await this.postcodeLocator.LocateDistrictAsync(firstDistrict);
            if (place == null)
            {
                return ServiceAreaSaveResult.Failed(nameof(model.PostcodeDistricts), $"We could not find where {firstDistrict} is. Check the postcode districts.");
            }

            var area = new ServiceArea
            {
                Slug = slug,
                Name = name,
                DriveTimeMinutes = await this.DriveMinutesFromBaseAsync(place.Value),
                IsFeatured = model.IsFeatured,
                IntroCopy = model.IntroCopy,
                LocalNeighbourhoodsCopy = model.LocalNeighbourhoodsCopy,
                PostcodeDistricts = districts,
                DiagramBearing = BearingFromBase(place.Value),
            };

            await this.areasRepository.AddAsync(area);
            await this.areasRepository.SaveChangesAsync();

            await this.ReplaceFaqsAsync(area.Id, model.Faqs);

            return ServiceAreaSaveResult.Saved(area.Id);
        }

        public async Task<ServiceAreaSaveResult> UpdateAsync(Guid id, ServiceAreaAdminInputModel model)
        {
            ServiceArea area = await this.areasRepository.All().FirstOrDefaultAsync(x => x.Id == id);
            if (area == null)
            {
                return ServiceAreaSaveResult.Failed(string.Empty, "This area no longer exists.");
            }

            // The page address and the drive time stay as they are: neither is on the form.
            string name = model.Name.Trim();
            string districts = await this.DistrictsOrTownsAsync(model.PostcodeDistricts, name);
            if (districts == null)
            {
                return TownNotFound(name);
            }

            area.Name = name;
            area.IsFeatured = model.IsFeatured;
            area.IntroCopy = model.IntroCopy;
            area.LocalNeighbourhoodsCopy = model.LocalNeighbourhoodsCopy;
            bool districtsChanged = districts != area.PostcodeDistricts;
            area.PostcodeDistricts = districts;
            area.DiagramLabelSide = model.DiagramLabelSide;

            // Only when the districts change, or the area is not on the diagram yet (so saving
            // again tries again): a place set by hand stays as it is.
            if (districtsChanged || area.DiagramBearing == null)
            {
                await this.PlaceOnDiagramAsync(area);
            }

            await this.areasRepository.SaveChangesAsync();

            await this.ReplaceFaqsAsync(id, model.Faqs);

            return ServiceAreaSaveResult.Saved(id);
        }

        public Task<bool> IsOnDiagramAsync(Guid id)
        {
            return this.areasRepository.AllAsNoTracking().AnyAsync(x => x.Id == id && x.DiagramBearing != null);
        }

        public async Task SetHeroImageAsync(Guid areaId, IFormFile imageFile)
        {
            if (imageFile == null || imageFile.Length == 0)
            {
                return;
            }

            ServiceArea area = await this.areasRepository.All().FirstOrDefaultAsync(x => x.Id == areaId);
            if (area == null)
            {
                return;
            }

            string imageUrl;
            using (Stream stream = imageFile.OpenReadStream())
            {
                imageUrl = await this.imageStorageService.SaveAreaImageAsync(stream, imageFile.FileName, imageFile.ContentType, area.Slug);
            }

            if (string.IsNullOrEmpty(imageUrl))
            {
                return;
            }

            string previous = area.HeroImageUrl;
            area.HeroImageUrl = imageUrl;
            await this.areasRepository.SaveChangesAsync();

            if (previous != null && previous != imageUrl)
            {
                await this.imageStorageService.DeleteAreaImageAsync(previous);
            }
        }

        public async Task<IReadOnlyList<string>> GetServedPostcodeDistrictsAsync()
        {
            List<string> lists = await this.areasRepository.All()
                .Where(x => x.PostcodeDistricts != null && x.PostcodeDistricts != string.Empty)
                .Select(x => x.PostcodeDistricts)
                .ToListAsync();

            // The areas' own districts and every district within 15 miles of the base
            // (PROJECT_STATE.md Section 3ek): a town added in the admin panel is taken even
            // when it lies past the 15 miles.
            return lists
                .SelectMany(UkPostcode.ParseDistricts)
                .Concat(BookingRadius.Districts)
                .Distinct()
                .OrderBy(d => d)
                .ToList();
        }

        public async Task<bool> IsPostcodeServedAsync(string postcode)
        {
            IReadOnlyList<string> districts = await this.GetServedPostcodeDistrictsAsync();
            return UkPostcode.IsInDistricts(postcode, districts);
        }

        public async Task DeleteAsync(Guid id)
        {
            ServiceArea area = await this.areasRepository.AllWithDeleted().FirstOrDefaultAsync(x => x.Id == id);
            if (area == null)
            {
                return;
            }

            // The FK from ServiceAreaFaq is ReferentialAction.Restrict, so the children have to go
            // first or SaveChanges throws a constraint violation.
            List<ServiceAreaFaq> faqs = await this.faqsRepository.AllWithDeleted()
                .Where(x => x.ServiceAreaId == id)
                .ToListAsync();

            foreach (ServiceAreaFaq faq in faqs)
            {
                this.faqsRepository.HardDelete(faq);
            }

            await this.faqsRepository.SaveChangesAsync();

            var picture = area.HeroImageUrl;
            this.areasRepository.HardDelete(area);
            await this.areasRepository.SaveChangesAsync();

            // After the area is gone, so a picture is never missing from an area still shown.
            if (picture != null)
            {
                await this.imageStorageService.DeleteAreaImageAsync(picture);
            }
        }

        public IEnumerable<ServiceAreaFaqValidationError> PruneAndValidateFaqs(ServiceAreaAdminInputModel model)
        {
            model.Faqs ??= new List<ServiceAreaFaqInputModel>();

            model.Faqs = model.Faqs
                .Where(f => !string.IsNullOrWhiteSpace(f?.Question) || !string.IsNullOrWhiteSpace(f?.Answer))
                .ToList();

            var errors = new List<ServiceAreaFaqValidationError>();

            for (var i = 0; i < model.Faqs.Count; i++)
            {
                var question = model.Faqs[i].Question?.Trim();
                var answer = model.Faqs[i].Answer?.Trim();

                // Limits mirror the ServiceAreaFaq entity, so a row that passes here cannot fail
                // at SaveChanges.
                if (string.IsNullOrWhiteSpace(question))
                {
                    errors.Add(new ServiceAreaFaqValidationError { Key = $"Faqs[{i}].Question", Message = "Question is required." });
                }
                else if (question.Length < 5 || question.Length > 300)
                {
                    errors.Add(new ServiceAreaFaqValidationError { Key = $"Faqs[{i}].Question", Message = "Question must be between 5 and 300 characters." });
                }

                if (string.IsNullOrWhiteSpace(answer))
                {
                    errors.Add(new ServiceAreaFaqValidationError { Key = $"Faqs[{i}].Answer", Message = "Answer is required." });
                }
                else if (answer.Length < 5 || answer.Length > 1000)
                {
                    errors.Add(new ServiceAreaFaqValidationError { Key = $"Faqs[{i}].Answer", Message = "Answer must be between 5 and 1000 characters." });
                }
            }

            return errors;
        }

        // "Walton-on-Thames & Weybridge" becomes "walton-on-thames-and-weybridge": lower case, "&"
        // as "and", and every run of anything else but letters and digits as one hyphen. Only
        // plain a to z is kept, so the address never needs escaping.
        private static string SlugFromName(string name)
        {
            string slug = Regex.Replace(name.ToLowerInvariant().Replace("&", " and "), "[^a-z0-9]+", "-");
            return slug.Trim('-');
        }

        private static ServiceAreaSaveResult TownNotFound(string name)
        {
            return ServiceAreaSaveResult.Failed(nameof(ServiceAreaAdminInputModel.Name), $"We could not find a town called {name}. Check the spelling.");
        }

        // Whole degrees: the diagram cannot show finer, and the admin page reads better.
        private static double BearingFromBase(GeoPoint place)
        {
            return Math.Round(DiagramCentre.BearingTo(place)) % 360;
        }

        // "kt5,KT6 , kt7" is saved as "KT5, KT6, KT7"; an empty box as no list at all.
        private static string NormalizeDistricts(string districts)
        {
            IReadOnlyList<string> parsed = UkPostcode.ParseDistricts(districts);
            return parsed.Count == 0 ? null : string.Join(", ", parsed);
        }

        /// <summary>
        /// The districts typed on the form, tidied; when the box is empty, the district of the town
        /// with this name (PROJECT_STATE.md Section 3dn). Null when the box is empty and no town
        /// has the name.
        /// </summary>
        private async Task<string> DistrictsOrTownsAsync(string typed, string townName)
        {
            return NormalizeDistricts(typed)
                ?? NormalizeDistricts(await this.postcodeLocator.FindTownDistrictAsync(townName, DiagramCentre));
        }

        /// <summary>
        /// Minutes by car from the base. When the route service does not answer, an estimate from
        /// the straight-line distance: about 5 minutes plus 1.1 a kilometre, which came within a
        /// few minutes of the 14 times first set by hand. Never 0, which the site's pages show as
        /// the base itself.
        /// </summary>
        private async Task<int> DriveMinutesFromBaseAsync(GeoPoint place)
        {
            int minutes = await this.driveTimeCalculator.DriveMinutesAsync(DiagramCentre, place)
                ?? (int)Math.Round(5 + (1.1 * DiagramCentre.DistanceKmTo(place)));

            return Math.Clamp(minutes, 1, 180);
        }

        /// <summary>
        /// Sets the area's direction on the coverage diagram from where its first postcode
        /// district is (PROJECT_STATE.md Section 3dm). No district, or none found: the direction
        /// is cleared and the area is left off the diagram, never a guessed place.
        /// </summary>
        private async Task PlaceOnDiagramAsync(ServiceArea area)
        {
            string firstDistrict = UkPostcode.ParseDistricts(area.PostcodeDistricts).FirstOrDefault();
            GeoPoint? place = firstDistrict == null ? null : await this.postcodeLocator.LocateDistrictAsync(firstDistrict);
            area.DiagramBearing = place == null ? null : BearingFromBase(place.Value);
        }

        /// <summary>
        /// FAQs are replaced wholesale rather than diffed. They carry no external references and
        /// are ordered purely by DisplayOrder, so recreating them is simpler than reconciling by id
        /// - and hard-deleting the old rows stops soft-deleted orphans accumulating on every save.
        /// </summary>
        private async Task ReplaceFaqsAsync(Guid areaId, IEnumerable<ServiceAreaFaqInputModel> faqs)
        {
            List<ServiceAreaFaq> existing = await this.faqsRepository.AllWithDeleted()
                .Where(x => x.ServiceAreaId == areaId)
                .ToListAsync();

            foreach (ServiceAreaFaq faq in existing)
            {
                this.faqsRepository.HardDelete(faq);
            }

            var displayOrder = 1;
            foreach (ServiceAreaFaqInputModel faq in faqs ?? Enumerable.Empty<ServiceAreaFaqInputModel>())
            {
                if (string.IsNullOrWhiteSpace(faq?.Question) || string.IsNullOrWhiteSpace(faq.Answer))
                {
                    continue;
                }

                await this.faqsRepository.AddAsync(new ServiceAreaFaq
                {
                    ServiceAreaId = areaId,
                    Question = faq.Question.Trim(),
                    Answer = faq.Answer.Trim(),
                    DisplayOrder = displayOrder++,
                });
            }

            await this.faqsRepository.SaveChangesAsync();
        }
    }
}
