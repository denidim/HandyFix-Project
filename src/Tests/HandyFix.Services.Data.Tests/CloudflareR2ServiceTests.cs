namespace HandyFix.Services.Data.Tests
{
    using HandyFix.Services;

    using Xunit;

    // Deleting an enquiry removes its photos from storage (PROJECT_STATE.md Section 3cb). Only the
    // photo's address is saved, so the object to remove is worked out from the address.
    public class CloudflareR2ServiceTests
    {
        private const string PublicUrl = "https://photos.example";

        [Theory]
        [InlineData("https://photos.example/inquiries/abc_tap.jpg", "inquiries/abc_tap.jpg")]
        [InlineData("https://photos.example/bookings/abc_tap.jpg", "bookings/abc_tap.jpg")]

        // The name the visitor's file had is part of the key exactly as it was sent, spaces and all.
        [InlineData("https://photos.example/inquiries/abc_my photo (1).jpg", "inquiries/abc_my photo (1).jpg")]
        [InlineData("https://photos.example/inquiries/abc_100%25 done.jpg", "inquiries/abc_100%25 done.jpg")]
        public void GetObjectKeyShouldBeWhatFollowsTheBucketsPublicAddress(string fileUrl, string expected)
        {
            Assert.Equal(expected, CloudflareR2Service.GetObjectKey(PublicUrl, fileUrl));
            Assert.Equal(expected, CloudflareR2Service.GetObjectKey(PublicUrl + "/", fileUrl));
        }

        // The bucket can be given another public address, a domain of its own for one. A photo
        // saved before that keeps its old address, and its path is still its key.
        [Theory]
        [InlineData("https://pub-1234.r2.dev/inquiries/abc_tap.jpg", "inquiries/abc_tap.jpg")]
        [InlineData("https://pub-1234.r2.dev/inquiries/abc_my%20photo.jpg", "inquiries/abc_my photo.jpg")]
        public void GetObjectKeyShouldFallBackToThePathForAnAddressOnAnotherHost(string fileUrl, string expected)
        {
            Assert.Equal(expected, CloudflareR2Service.GetObjectKey(PublicUrl, fileUrl));
        }

        // Upload paths from before photos went to R2, and anything else that is not a web address,
        // are not objects in the bucket.
        [Theory]
        [InlineData("/uploads/inquiries/some_image_file.jpg")]
        [InlineData("uploads/some_image_file.jpg")]
        [InlineData("ftp://photos.example/inquiries/abc_tap.jpg")]
        [InlineData("")]
        [InlineData(null)]
        public void GetObjectKeyShouldBeNullForWhatIsNotAWebAddress(string fileUrl)
        {
            Assert.Null(CloudflareR2Service.GetObjectKey(PublicUrl, fileUrl));
        }
    }
}
