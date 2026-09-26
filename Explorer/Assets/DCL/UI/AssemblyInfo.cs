using System.Runtime.CompilerServices;

// Exposes internal members (e.g. ChangeRealmPromptController.DestinationHostFor) to the EditMode test assembly
// so security-critical host parsing can be unit-tested without widening the members to public.
[assembly: InternalsVisibleTo("DCL.EditMode.Tests")]
