// This file is part of Silk.NET.
// 
// You may modify and distribute Silk.NET under the terms
// of the MIT license. See the LICENSE file for details.

using Silk.NET.Windowing.Common;

namespace Silk.NET.Windowing
{
    /// <summary>
    /// Convenience wrapper for easily creating a Silk.NET window.
    /// </summary>
    public static class Window
    {
        /// <summary>
        /// Create a window on the current platform.
        /// </summary>
        /// <param name="options">The window to use.</param>
        /// <returns>A Silk.NET window using the current platform.</returns>
        public static IWindow Create(WindowOptions options)
        {
            if (Silk.CurrentPlatform == null) {
                Silk.Init();
            }

            // We should have a platform now, as Silk.Init would've thrown otherwise.
            // ReSharper disable once PossibleNullReferenceException
            return Silk.CurrentPlatform.GetWindow(options);
        }
    }
}