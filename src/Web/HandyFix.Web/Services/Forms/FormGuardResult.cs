namespace HandyFix.Web.Services.Forms
{
    // What the form guard made of a submission.
    public enum FormGuardResult
    {
        // Nothing against it: handle it as a person's.
        Passed = 0,

        // It carries a mark only a program leaves: the hidden field filled in, or sent faster
        // than anyone types. It is answered exactly like a real one and nothing is saved, so the
        // program learns nothing from the reply.
        Automated = 1,
    }
}
