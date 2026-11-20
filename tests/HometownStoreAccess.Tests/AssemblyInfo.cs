using Xunit;

// Several tests switch the mod's language (shared static state): run them one at a time.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
