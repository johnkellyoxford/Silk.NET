// This file is part of Silk.NET.
// 
// You may modify and distribute Silk.NET under the terms
// of the MIT license. See the LICENSE file for details.

namespace Silk.NET.Input.Common
{
    /// <summary>
    /// The deadzone to use for a joystick/gamepad's sticks.
    /// </summary>
    public struct Deadzone
    {
        /// <summary>
        /// The size of the deadzone to use.
        /// </summary>
        public float Value { get; }
        
        /// <summary>
        /// The deadzone method to use.
        /// </summary>
        public DeadzoneMethod Method { get; }

        /// <summary>
        /// Creates a new instance of the Deadzone struct.
        /// </summary>
        /// <param name="value">The deadzone size.</param>
        /// <param name="method">The deadzone method.</param>
        public Deadzone(float value, DeadzoneMethod method)
        {
            Value = value;
            Method = method;
        }
    }
}