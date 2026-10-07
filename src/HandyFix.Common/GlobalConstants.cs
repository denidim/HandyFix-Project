namespace HandyFix.Common
{
    public static class GlobalConstants
    {
        public const string SystemName = "Plumbing Handyman Surrey";

        // The business's public contact details, kept once here so every page shows the same ones
        // (PROJECT_STATE.md Section 3bu). The line is a VoIP landline: written messages go to
        // WhatsApp on the same number, so nothing on the site offers SMS.
        public const string BusinessPhone = "020 3951 5915";

        // For tel: links and JSON-LD. Inside a script block write it with Json.Serialize, like any
        // other value there: Razor's own HTML encoding turns the "+" into an entity, and a script
        // block never decodes entities.
        public const string BusinessPhoneInternational = "+442039515915";

        public const string BusinessWhatsAppUrl = "https://wa.me/442039515915";

        public const string BusinessEmail = "info@plumbing-handyman-surrey.co.uk";

        // The limited company the business trades under, as on the Companies House register.
        public const string CompanyLegalName = "ZAP80 LTD";

        public const string CompanyNumber = "12658426";

        // The company's registered office, which is also the business address in the structured data.
        public const string CompanyAddressStreet = "16 Stormont Way";

        public const string CompanyAddressTown = "Chessington";

        public const string CompanyAddressCounty = "Surrey";

        public const string CompanyAddressPostcode = "KT9 2QN";

        public const string CompanyAddress = CompanyAddressStreet + ", " + CompanyAddressTown + ", " + CompanyAddressCounty + ", " + CompanyAddressPostcode;

        // The day the company was incorporated, in the ISO form JSON-LD expects.
        public const string CompanyIncorporatedOn = "2020-06-09";

        public const string AdministratorRoleName = "Administrator";
    }
}
