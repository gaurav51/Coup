using System.Runtime.CompilerServices;

// The test assembly builds exact hands directly, which keeps rules tests readable
// instead of hunting for a deck seed that happens to deal the case under test.
[assembly: InternalsVisibleTo("Coup.Core.Tests")]
