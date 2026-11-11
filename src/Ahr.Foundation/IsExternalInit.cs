#if NETSTANDARD
using System.Diagnostics.CodeAnalysis;

namespace System.Runtime.CompilerServices;

[SuppressMessage("Major Code Smell", "S2094:Classes should not be empty", Justification = "Compiler polyfill marker type.")]
internal static class IsExternalInit
{
}
#endif
