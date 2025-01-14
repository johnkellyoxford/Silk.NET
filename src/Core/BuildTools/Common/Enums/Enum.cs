// This file is part of Silk.NET.
// 
// You may modify and distribute Silk.NET under the terms
// of the MIT license. See the LICENSE file for details.

using System.Collections.Generic;

namespace Silk.NET.BuildTools.Common.Enums
{
    /// <summary>
    /// Represents a C# enum.
    /// </summary>
    public class Enum
    {
        /// <summary>
        /// Gets or sets a list of tokens contained within this enum.
        /// </summary>
        public List<Token> Tokens { get; set; } = new List<Token>();

        /// <summary>
        /// Gets or sets the name of this enum.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the name of this enum as defined by the Khronos spec.
        /// </summary>
        public string NativeName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the extension to which this enum belongs. Generally, this is either Core or the
        /// enum's <see cref="NativeName"/>.
        /// </summary>
        public string ExtensionName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a list of attributes.
        /// </summary>
        public List<Attribute> Attributes { get; set; } = new List<Attribute>();
    }
}
