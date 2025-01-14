// This file is part of Silk.NET.
// 
// You may modify and distribute Silk.NET under the terms
// of the MIT license. See the LICENSE file for details.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using JetBrains.Annotations;
using Silk.NET.BuildTools.Common;
using Silk.NET.BuildTools.Common.Enums;
using Silk.NET.BuildTools.Common.Functions;
using Silk.NET.BuildTools.Convert.XML;
using Attribute = Silk.NET.BuildTools.Common.Attribute;
using Enum = Silk.NET.BuildTools.Common.Enums.Enum;

namespace Silk.NET.BuildTools.Convert.Construction
{
    /// <summary>
    /// A collection of extension methods for parsing and constructing profiles.
    /// </summary>
    public static class ProfileConstructorExtensions
    {
        /// <summary>
        /// Parses an enum signature from XML.
        /// </summary>
        /// <param name="element">The XML to parse.</param>
        /// <returns>The resultant enum.</returns>
        public static Enum ParseEnum(XElement element)
        {
            var result = new Enum
            {
                Name = NativeIdentifierTranslator.TranslateIdentifierName
                (
                    element.Attribute("name")?.Value.CheckMemberName(Converter.CliOptions.Prefix)
                    ?? throw new InvalidOperationException("No name attribute.")
                ),
                NativeName = element.Attribute("name")?.Value,
                ExtensionName = element.Attribute("extension")?.Value
            };
            foreach (var child in element.Elements("token"))
            {
                var deprecatedSince = ParsingHelpers.ParseVersion(child, "deprecated");
                result.Tokens.Add
                (
                    new Token
                    {
                        Name = NativeIdentifierTranslator.TranslateIdentifierName(child.Attribute("name")?.Value)
                            .CheckMemberName(Converter.CliOptions.Prefix),
                        NativeName = child.Attribute("name")?.Value,
                        Value = FormatToken(child.Attribute("value")?.Value),
                        Attributes = deprecatedSince != null
                            ? new List<Attribute>
                            {
                                new Attribute
                                {
                                    Name = "Obsolete",
                                    Arguments = new List<string> {"\"Deprecated in " + deprecatedSince + ".\""}
                                }
                            }
                            : new List<Attribute>()
                    }
                );
            }

            return result;
        }

        /// <summary>
        /// Parses a function from XML.
        /// </summary>
        /// <param name="element">The XML to parse.</param>
        /// <returns>The resultant function.</returns>
        public static Function ParseFunction(XElement element)
        {
            var functionName = element.GetRequiredAttribute("name").Value;
            var functionCategories = element.GetRequiredAttribute("category")
                .Value
                .Split(new[] {'|'}, StringSplitOptions.RemoveEmptyEntries);
            var functionExtensions = element.GetRequiredAttribute("extension").Value;
            
            var functionDeprecationVersion = ParsingHelpers.ParseVersion(element, "deprecated");

            var parameters = ParseParameters(element);

            var returnElement = element.GetRequiredElement("returns");
            var returnType = ParsingHelpers.ParseTypeSignature(returnElement);
            return new Function
            {
                Name = NameTrimmer.Trim(functionName).CheckMemberName(Converter.CliOptions.Prefix),
                NativeName = functionName,
                Parameters = parameters.ToList(),
                ReturnType = returnType,
                Categories = functionCategories,
                ExtensionName = functionExtensions,
                Attributes = functionDeprecationVersion != null
                    ? new List<Attribute>
                    {
                        new Attribute
                        {
                            Name = "Obsolete",
                            Arguments = new List<string> {"\"Deprecated in " + functionDeprecationVersion + ".\""}
                        }
                    }
                    : new List<Attribute>()
            };
        }

        /// <summary>
        /// Parses parameters from XML.
        /// </summary>
        /// <param name="functionElement">The XML block containing the parameters.</param>
        /// <returns>The parsed parameters.</returns>
        [NotNull]
        [ItemNotNull]
        private static IReadOnlyList<Parameter> ParseParameters([NotNull] XElement functionElement)
        {
            var parameterElements = functionElement.Elements().Where(e => e.Name == "param");
            var parametersWithComputedCounts =
                new List<(Parameter Parameter, IReadOnlyList<string> ComputedCountParameterNames)>();
            var parametersWithValueReferenceCounts =
                new List<(Parameter Parameter, string ParameterReferenceName)>();

            var resultParameters = new List<Parameter>();

            foreach (var parameterElement in parameterElements)
            {
                var parameter = ParseParameter
                (
                    parameterElement,
                    out var hasComputedCount,
                    out var computedCountParameterNames,
                    out var hasValueReference,
                    out var valueReferenceName,
                    out var _
                );

                if (hasComputedCount)
                {
                    parametersWithComputedCounts.Add((parameter, computedCountParameterNames));
                }

                if (hasValueReference)
                {
                    parametersWithValueReferenceCounts.Add((parameter, valueReferenceName));

                    // TODO: Pass on the mathematical expression
                }

                resultParameters.Add(parameter);
            }

            return resultParameters;
        }

        /// <summary>
        /// Parses a function parameter signature from the given <see cref="XElement" />.
        /// </summary>
        /// <param name="paramElement">The parameter element.</param>
        /// <param name="hasComputedCount">Whether or not the parameter has a computed count.</param>
        /// <param name="computedCountParameterNames">
        /// The names of the parameters that the count is computed from, if any.
        /// </param>
        /// <param name="hasValueReference">Whether or not the parameter has a count value reference.</param>
        /// <param name="valueReferenceName">The name of the parameter that the count value references.</param>
        /// <param name="valueReferenceExpression">The expression that should be applied to the value reference.</param>
        /// <returns>A parsed parameter.</returns>
        [NotNull]
        [ContractAnnotation
            (
                "hasComputedCount : true => computedCountParameterNames : notnull;" +
                "hasValueReference : true => valueReferenceName : notnull"
            )
        ]
        private static Parameter ParseParameter
        (
            [NotNull] XElement paramElement,
            out bool hasComputedCount,
            [CanBeNull] out IReadOnlyList<string> computedCountParameterNames,
            out bool hasValueReference,
            [CanBeNull] out string valueReferenceName,
            [CanBeNull] out string valueReferenceExpression
        )
        {
            var paramName = paramElement.GetRequiredAttribute("name").Value;

            // A parameter is technically a type signature (think of it as Parameter : ITypeSignature)
            var paramType = ParsingHelpers.ParseTypeSignature(paramElement);

            var paramFlowStr = paramElement.GetRequiredAttribute("flow").Value;

            if (!System.Enum.TryParse<FlowDirection>(paramFlowStr, true, out var paramFlow))
            {
                throw new InvalidDataException("Could not parse the parameter flow.");
            }

            var paramCountStr = paramElement.Attribute("count")?.Value;
            var countSignature = ParsingHelpers.ParseCountSignature
            (
                paramCountStr,
                out hasComputedCount,
                out computedCountParameterNames,
                out hasValueReference,
                out valueReferenceName,
                out valueReferenceExpression
            );

            return new Parameter
            {
                Name = Utilities.CSharpKeywords.Contains(paramName) ? "@" + paramName : paramName,
                Flow = paramFlow,
                Type = paramType,
                Count = countSignature
            };
        }

        /// <summary>
        /// Parses the enums and functions from their XML representations, and writes them to this profile.
        /// </summary>
        /// <param name="profile">The profile to write to.</param>
        /// <param name="enums">The enum XML blocks.</param>
        /// <param name="functions">The function XML blocks.</param>
        public static void ParseXml(this Profile profile, IEnumerable<XElement> enums, IEnumerable<XElement> functions)
        {
            profile.Projects.Add
            (
                "Core",
                new Project {CategoryName = "Core", ExtensionName = "Core", IsRoot = true, Namespace = string.Empty}
            );
            var funs = functions.ToList();
            var parsedFunctions = funs.Select(ParseFunction).ToList();
            var parsedEnums = enums.Select(ParseEnum).ToList();
            foreach (var typeMap in profile.TypeMaps)
            {
                TypeMapper.Map(typeMap, parsedFunctions);
            }

            profile.WriteFunctions(parsedFunctions);
            profile.WriteEnums(parsedEnums);
            
            TypeMapper.MapEnums(profile);
        }

        /// <summary>
        /// Writes a collection of enums to their appropriate projects.
        /// </summary>
        /// <param name="profile">The profile to write the projects to.</param>
        /// <param name="enums">The enums to write.</param>
        public static void WriteEnums(this Profile profile, IEnumerable<Enum> enums)
        {
            var mergedEnums = new Dictionary<string, Enum>();
            var gl = profile.ClassName.ToUpper().CheckMemberName(Converter.CliOptions.Prefix);
            mergedEnums.Add(gl + "Enum", new Enum() {Name = gl + "Enum", ExtensionName = "Core"});
            
            // first, we need to categorise the enums into "Core", or their vendor (i.e. "NV", "SGI", "KHR" etc)
            foreach (var @enum in enums)
            {
                if (@enum.ExtensionName == "Core")
                {
                    mergedEnums[gl + "Enum"].Tokens.AddRange(@enum.Tokens);
                }
                else
                {
                    var suffix = FormatCategory(@enum.ExtensionName);
                    if (!mergedEnums.ContainsKey(suffix))
                    {
                        mergedEnums.Add
                        (
                            suffix,
                            new Enum{Name = suffix.CheckMemberName(Converter.CliOptions.Prefix), ExtensionName = suffix}
                        );
                    }
                    mergedEnums[suffix].Tokens.AddRange(@enum.Tokens);
                }
            }
            
            // now that we've categorised them, lets add them into their appropriate projects.
            foreach (var (_, @enum) in mergedEnums)
            {
                if (!profile.Projects.ContainsKey(@enum.ExtensionName))
                {
                    profile.Projects.Add
                    (
                        @enum.ExtensionName,
                        new Project
                        {
                            CategoryName = @enum.ExtensionName, ExtensionName = @enum.ExtensionName, IsRoot = false,
                            Namespace = "." + @enum.ExtensionName.CheckMemberName(Converter.CliOptions.Prefix)
                        }
                    );
                }

                profile.Projects[@enum.ExtensionName].Enums.Add(@enum);
            }
        }

        /// <summary>
        /// Writes a collection of functions to their appropriate projects.
        /// </summary>
        /// <param name="profile">The profile to write the projects to.</param>
        /// <param name="functions">The functions to write.</param>
        public static void WriteFunctions(this Profile profile, IEnumerable<Function> functions)
        {
            foreach (var function in functions)
            {
                foreach (var rawCategory in function.Categories)
                {
                    var category = FormatCategory(rawCategory);
                    // check that the root project exists
                    if (!profile.Projects.ContainsKey("Core"))
                    {
                        profile.Projects.Add
                        (
                            "Core",
                            new Project
                            {
                                CategoryName = "Core", ExtensionName = "Core", IsRoot = true,
                                Namespace = string.Empty
                            }
                        );
                    }

                    // check that the extension project exists, if applicable
                    if (function.ExtensionName != "Core" && !profile.Projects.ContainsKey(category))
                    {
                        profile.Projects.Add
                        (
                            category,
                            new Project
                            {
                                CategoryName = category, ExtensionName = category, IsRoot = false,
                                Namespace = "." + category.CheckMemberName(Converter.CliOptions.Prefix)
                            }
                        );
                    }

                    // check that the interface exists
                    if
                    (
                        !profile.Projects[function.ExtensionName == "Core" ? "Core" : category]
                            .Interfaces.ContainsKey(rawCategory)
                    )
                    {
                        profile.Projects[function.ExtensionName == "Core" ? "Core" : category]
                            .Interfaces.Add
                            (
                                rawCategory,
                                new Interface
                                {
                                    Name = "I" + NativeIdentifierTranslator.TranslateIdentifierName(rawCategory)
                                        .CheckMemberName(Converter.CliOptions.Prefix)
                                }
                            );
                    }

                    // add the function to the interface
                    profile.Projects[function.ExtensionName == "Core" ? "Core" : category]
                        .Interfaces[rawCategory]
                        .Functions.Add(function);
                }
            }
        }
        
        private static string FormatCategory(string rawCategory)
        {
            return rawCategory.Split('_').FirstOrDefault();
        }
        
        private static string FormatToken(string token)
        {
            if (token == null)
            {
                return null;
            }

            var tokenHex = token.StartsWith("0x") ? token.Substring(2) : token;

            if (!long.TryParse(tokenHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
            {
                if (!long.TryParse(tokenHex, out value))
                {
                    throw new InvalidDataException("Token value was not in a valid format.");
                }
            }

            var valueString = $"0x{value:X}";
            var needsCasting = value > int.MaxValue || value < 0;
            if (needsCasting)
            {
                Debug.WriteLine($"Warning: casting overflowing enum value {token} from 64-bit to 32-bit.");
                valueString = $"unchecked((int){valueString})";
            }

            return valueString;
        }
    }
}
