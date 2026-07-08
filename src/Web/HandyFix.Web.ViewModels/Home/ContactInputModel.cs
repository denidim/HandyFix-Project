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

        [Required(ErrorMessage = "Please enter your message.")]
        [MinLength(10, ErrorMessage = "Message must be at least 10 characters long.")]
        [MaxLength(3000, ErrorMessage = "Message cannot exceed 3000 characters.")]
        public string Message { get; set; }

        public List<IFormFile> Images { get; set; } = new List<IFormFile>();
    }
}
