// ReSharper disable VirtualMemberCallInConstructor
namespace HandyFix.Data.Models
{
    using System;

    using System.Collections.Generic;

    using System.ComponentModel.DataAnnotations;

    using HandyFix.Data.Common.Models;

    using Microsoft.AspNetCore.Identity;

    public class ApplicationUser : IdentityUser, IAuditInfo, IDeletableEntity
    {
        public ApplicationUser()
        {
            this.Id = Guid.NewGuid().ToString();
            this.Bookings = new HashSet<Booking>();
            this.Reviews = new HashSet<Review>();
            this.Roles = new HashSet<IdentityUserRole<string>>();
            this.Claims = new HashSet<IdentityUserClaim<string>>();
            this.Logins = new HashSet<IdentityUserLogin<string>>();
        }

        [Required]
        [MaxLength(100, ErrorMessage = "The {0} field cannot exceed 100 characters.")]
        [MinLength(2, ErrorMessage = "The {0} field must be at least 2 characters long.")]
        public string FirstName { get; set; }

        [MaxLength(100, ErrorMessage = "The {0} field cannot exceed 100 characters.")]
        [MinLength(2, ErrorMessage = "The {0} field must be at least 2 characters long.")]
        public string LastName { get; set; }

        public bool IsGuest { get; set; }

        // Audit info
        public DateTime CreatedOn { get; set; }

        public DateTime? ModifiedOn { get; set; }

        // Deletable entity
        public bool IsDeleted { get; set; }

        public DateTime? DeletedOn { get; set; }

        public virtual ICollection<Booking> Bookings { get; set; }

        public virtual ICollection<Review> Reviews { get; set; }

        public virtual ICollection<IdentityUserRole<string>> Roles { get; set; }

        public virtual ICollection<IdentityUserClaim<string>> Claims { get; set; }

        public virtual ICollection<IdentityUserLogin<string>> Logins { get; set; }
    }
}
