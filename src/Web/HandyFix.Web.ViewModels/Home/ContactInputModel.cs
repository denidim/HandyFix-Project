namespace HandyFix.Web.ViewModels.Home
{
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;

    using Microsoft.AspNetCore.Http;

    public class ContactInputModel
    {
        [Required(ErrorMessage = "Please enter your name.")]
        [MinLength(2, ErrorMessage = "Name must be at least 2 characters.")]
        [MaxLength(100, ErrorMessage = "Name cannot exceed 100 characters.")]
        public string Name { get; set; }

        [Required(ErrorMessage = "Please enter your email address.")]
        [EmailAddress(ErrorMessage = "Invalid email address format.")]
        [MaxLength(255, ErrorMessage = "Email cannot exceed 255 characters.")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Please enter your phone number.")]
        [Phone(ErrorMessage = "Invalid phone number format.")]
        [MaxLength(20, ErrorMessage = "Phone number cannot exceed 20 characters.")]
        public string PhoneNumber { get; set; }

        // 2900, not the column's 3000: InquiriesService prefixes "[Category: ...] " (up to ~65
        // characters) when saving, and the stored message still has to fit nvarchar(3000).
        [Required(ErrorMessage = "Please enter your message.")]
        [MinLength(10, ErrorMessage = "Message must be at least 10 characters long.")]
        [MaxLength(2900, ErrorMessage = "Message cannot exceed 2900 characters.")]
        public string Message { get; set; }

        [Required(ErrorMessage = "Please choose what your enquiry is about.")]
        [MaxLength(50, ErrorMessage = "Category cannot exceed 50 characters.")]
        public string Category { get; set; }

        public List<IFormFile> Images { get; set; } = new List<IFormFile>();
    }
}
