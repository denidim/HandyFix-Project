namespace HandyFix.Services.Data.Gallery
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Data.Common.Repositories;
    using HandyFix.Data.Models;
    using HandyFix.Services;
    using HandyFix.Services.Mapping;
    using HandyFix.Web.ViewModels.Gallery;

    using Microsoft.AspNetCore.Http;
    using Microsoft.EntityFrameworkCore;

    public class GalleryService : IGalleryService
    {
        public const int MaxBatchUploadCount = 20;
        public const long MaxFileSizeBytes = 15L * 1024 * 1024;

        // The photos of one batch together. The live site is behind Cloudflare, which lets at most
        // 100 MB through in one request; the upload action allows this plus room for the form's
        // other fields, and the admin page refuses more before anything is sent.
        public const long MaxBatchBytes = 90L * 1024 * 1024;

        public const long MaxUploadRequestBytes = MaxBatchBytes + (1024 * 1024);

        private readonly IRepository<GalleryPhoto> galleryRepository;
        private readonly IImageStorageService imageStorageService;

        public GalleryService(
            IRepository<GalleryPhoto> galleryRepository,
            IImageStorageService imageStorageService)
        {
            this.galleryRepository = galleryRepository;
            this.imageStorageService = imageStorageService;
        }

        public Task<IReadOnlyList<GalleryPhotoViewModel>> GetAllAsync() =>
            this.LoadPhotosAsync(homeOnly: false);

        public Task<IReadOnlyList<GalleryPhotoViewModel>> GetHomePhotosAsync() =>
            this.LoadPhotosAsync(homeOnly: true);

        public async Task<T> GetByIdAsync<T>(Guid id)
        {
            return await this.galleryRepository.AllAsNoTracking()
                .Where(p => p.Id == id)
                .To<T>()
                .FirstOrDefaultAsync();
        }

        public async Task<int> CreateBatchAsync(GalleryBatchUploadInputModel input)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            List<IFormFile> files = (input.Photos ?? Array.Empty<IFormFile>())
                .Where(f => f != null && f.Length > 0)
                .ToList();

            if (files.Count == 0)
            {
                throw new ImageUploadValidationException("Please select at least one photo to upload.");
            }

            if (files.Count > MaxBatchUploadCount)
            {
                throw new ImageUploadValidationException($"A maximum of {MaxBatchUploadCount} photos can be uploaded in one batch.");
            }

            foreach (IFormFile file in files)
            {
                if (file.Length > MaxFileSizeBytes)
                {
                    throw new ImageUploadValidationException(
                        $"File \"{file.FileName}\" exceeds the maximum allowed size of {MaxFileSizeBytes / (1024 * 1024)} MB.");
                }
            }

            string caption = (input.Caption ?? string.Empty).Trim();
            string badge = NormalizeBadge(input.StageBadge);

            // New photos go first, in the order they were picked: the newest work is what a visitor
            // should see first, and a starred one is among the home strip's first six at once.
            int nextOrder = 1;

            var uploadedUrls = new List<string>(files.Count);

            try
            {
                foreach (IFormFile file in files)
                {
                    using (Stream stream = file.OpenReadStream())
                    {
                        string imageUrl = await this.imageStorageService.SaveGalleryImageAsync(
                            stream,
                            file.FileName,
                            file.ContentType);

                        if (string.IsNullOrWhiteSpace(imageUrl))
                        {
                            throw new ImageUploadValidationException($"File \"{file.FileName}\" could not be uploaded.");
                        }

                        uploadedUrls.Add(imageUrl);

                        var entity = new GalleryPhoto
                        {
                            ImageUrl = imageUrl,
                            Caption = caption,
                            StageBadge = badge,
                            ShowOnHomePage = input.ShowOnHomePage,
                            DisplayOrder = nextOrder++,
                        };

                        await this.galleryRepository.AddAsync(entity);
                    }
                }

                // Every photo already there moves down behind the new ones, keeping its own order;
                // saved with them, in one go.
                List<GalleryPhoto> existing = await this.galleryRepository.All().ToListAsync();
                foreach (GalleryPhoto photo in existing)
                {
                    photo.DisplayOrder += uploadedUrls.Count;
                }

                await this.galleryRepository.SaveChangesAsync();
                return uploadedUrls.Count;
            }
            catch
            {
                foreach (string url in uploadedUrls)
                {
                    await this.imageStorageService.DeleteGalleryImageAsync(url);
                }

                throw;
            }
        }

        public async Task<bool> UpdateAsync(Guid id, GalleryPhotoEditInputModel input)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            GalleryPhoto photo = await this.galleryRepository.All().FirstOrDefaultAsync(p => p.Id == id);
            if (photo == null)
            {
                return false;
            }

            photo.Caption = (input.Caption ?? string.Empty).Trim();
            photo.StageBadge = NormalizeBadge(input.StageBadge);
            photo.ShowOnHomePage = input.ShowOnHomePage;
            photo.DisplayOrder = input.DisplayOrder;

            await this.galleryRepository.SaveChangesAsync();
            return true;
        }

        public async Task<bool?> ToggleHomePageAsync(Guid id)
        {
            GalleryPhoto photo = await this.galleryRepository.All().FirstOrDefaultAsync(p => p.Id == id);
            if (photo == null)
            {
                return null;
            }

            photo.ShowOnHomePage = !photo.ShowOnHomePage;
            await this.galleryRepository.SaveChangesAsync();
            return photo.ShowOnHomePage;
        }

        public async Task<bool> MoveAsync(Guid id, int direction)
        {
            List<GalleryPhoto> ordered = await this.galleryRepository.All()
                .OrderBy(p => p.DisplayOrder)
                .ThenByDescending(p => p.CreatedOn)
                .ToListAsync();

            int currentIndex = ordered.FindIndex(p => p.Id == id);
            if (currentIndex < 0)
            {
                return false;
            }

            int targetIndex = currentIndex + (direction < 0 ? -1 : 1);
            if (targetIndex < 0 || targetIndex >= ordered.Count)
            {
                return false;
            }

            (ordered[currentIndex], ordered[targetIndex]) = (ordered[targetIndex], ordered[currentIndex]);

            // Re-number sequentially so ties in DisplayOrder can never stall subsequent moves.
            for (int i = 0; i < ordered.Count; i++)
            {
                ordered[i].DisplayOrder = i + 1;
            }

            await this.galleryRepository.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            GalleryPhoto photo = await this.galleryRepository.All().FirstOrDefaultAsync(p => p.Id == id);
            if (photo == null)
            {
                return false;
            }

            string imageUrl = photo.ImageUrl;
            this.galleryRepository.Delete(photo);
            await this.galleryRepository.SaveChangesAsync();

            await this.imageStorageService.DeleteGalleryImageAsync(imageUrl);
            return true;
        }

        private static string NormalizeBadge(string stageBadge)
        {
            if (string.Equals(stageBadge?.Trim(), "Before", StringComparison.OrdinalIgnoreCase))
            {
                return "Before";
            }

            if (string.Equals(stageBadge?.Trim(), "After", StringComparison.OrdinalIgnoreCase))
            {
                return "After";
            }

            return null;
        }

        private async Task<IReadOnlyList<GalleryPhotoViewModel>> LoadPhotosAsync(bool homeOnly)
        {
            IQueryable<GalleryPhoto> query = this.galleryRepository.AllAsNoTracking();
            if (homeOnly)
            {
                query = query.Where(p => p.ShowOnHomePage);
            }

            List<GalleryPhotoViewModel> photos = await query
                .OrderBy(p => p.DisplayOrder)
                .ThenByDescending(p => p.CreatedOn)
                .To<GalleryPhotoViewModel>()
                .ToListAsync();

            foreach (GalleryPhotoViewModel photo in photos)
            {
                photo.CardImageUrl = this.imageStorageService.GetGalleryCardCopyPublicUrl(photo.ImageUrl) ?? photo.ImageUrl;
            }

            return photos;
        }
    }
}
