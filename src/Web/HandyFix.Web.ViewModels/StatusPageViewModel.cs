namespace HandyFix.Web.ViewModels
{
    // What the site says when a request ends in an error status and there is no page of its own
    // to show: a page that is not there, a form sent too many times, something that broke. Before
    // this a visitor got the browser's own blank error screen (PROJECT_STATE.md Section 3cb).
    public class StatusPageViewModel
    {
        public int Code { get; private set; }

        // For the browser tab.
        public string Title { get; private set; }

        public string Heading { get; private set; }

        public string Message { get; private set; }

        public static StatusPageViewModel For(int code)
        {
            return code switch
            {
                404 => new StatusPageViewModel
                {
                    Code = 404,
                    Title = "Page Not Found",
                    Heading = "We can't find that page.",
                    Message = "The address may be mistyped, or the page may have moved. The links below will get you back on track.",
                },
                429 => new StatusPageViewModel
                {
                    Code = 429,
                    Title = "Too Many Attempts",
                    Heading = "That's a lot of tries in a short while.",
                    Message = "To keep automated spam out, a form can only be sent so many times in a few minutes. Please wait ten minutes and try again, or call or message us now and we'll help you straight away.",
                },
                400 => new StatusPageViewModel
                {
                    Code = 400,
                    Title = "That Didn't Go Through",
                    Heading = "That didn't go through.",
                    Message = "The page had probably been open for a while and its form had expired. Go back, refresh the page and send it again. Nothing was sent or charged.",
                },
                403 => new StatusPageViewModel
                {
                    Code = 403,
                    Title = "No Access",
                    Heading = "This page isn't open to you.",
                    Message = "If you think it should be, sign in with an account that has access, or get in touch.",
                },
                _ when code >= 500 => new StatusPageViewModel
                {
                    Code = code,
                    Title = "Something Went Wrong",
                    Heading = "Something went wrong on our side.",
                    Message = "It isn't anything you did. Please try again in a minute. If you were booking or sending us a message and aren't sure it arrived, call or message us and we'll check.",
                },
                _ => new StatusPageViewModel
                {
                    Code = code,
                    Title = "That Didn't Work",
                    Heading = "That didn't work.",
                    Message = "Please go back and try again, or use the links below.",
                },
            };
        }
    }
}
