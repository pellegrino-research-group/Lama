using System;
using System.Collections.Generic;
using System.Linq;

namespace Lama.Core.Model.Steps
{
    /// <summary>
    /// Explicit output request for a step (file/print and nodal/element scope).
    /// </summary>
    public sealed class StepOutputRequest
    {
        public StepOutputType OutputType { get; }
        public IReadOnlyList<string> Variables { get; }
        public string TargetSetName { get; }

        private StepOutputRequest(StepOutputType outputType, IEnumerable<string> variables, string targetSetName = null)
        {
            var variableList = variables?
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v => v.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToList()
                ?? throw new ArgumentNullException(nameof(variables));

            if (variableList.Count == 0)
                throw new ArgumentException("At least one output variable is required.", nameof(variables));

            OutputType = outputType;
            Variables = variableList;
            TargetSetName = string.IsNullOrWhiteSpace(targetSetName) ? null : targetSetName.Trim();
        }

        /// <summary>
        /// Creates a <c>*NODE FILE</c> request.
        /// </summary>
        public static StepOutputRequest NodeFile(params NodalOutputVariable[] variables) =>
            new StepOutputRequest(StepOutputType.NodeFile, variables.Select(v => v.ToString()));

        public static StepOutputRequest NodeFileRaw(params string[] variables) =>
            new StepOutputRequest(StepOutputType.NodeFile, variables);

        /// <summary>
        /// Creates an <c>*EL FILE</c> request.
        /// </summary>
        public static StepOutputRequest ElementFile(params ElementOutputVariable[] variables) =>
            new StepOutputRequest(StepOutputType.ElementFile, variables.Select(v => v.ToString()));

        public static StepOutputRequest ElementFileRaw(params string[] variables) =>
            new StepOutputRequest(StepOutputType.ElementFile, variables);

        /// <summary>
        /// Creates a <c>*NODE PRINT</c> request.
        /// </summary>
        public static StepOutputRequest NodePrint(params NodalOutputVariable[] variables) =>
            new StepOutputRequest(StepOutputType.NodePrint, variables.Select(v => v.ToString()));

        public static StepOutputRequest NodePrintRaw(params string[] variables) =>
            new StepOutputRequest(StepOutputType.NodePrint, variables);

        /// <summary>
        /// Creates a <c>*NODE PRINT,NSET=...</c> request.
        /// </summary>
        public static StepOutputRequest NodePrint(string nodeSetName, params NodalOutputVariable[] variables) =>
            new StepOutputRequest(StepOutputType.NodePrint, variables.Select(v => v.ToString()), nodeSetName);

        public static StepOutputRequest NodePrintRaw(string nodeSetName, params string[] variables) =>
            new StepOutputRequest(StepOutputType.NodePrint, variables, nodeSetName);

        /// <summary>
        /// Creates an <c>*EL PRINT</c> request.
        /// </summary>
        public static StepOutputRequest ElementPrint(params ElementOutputVariable[] variables) =>
            new StepOutputRequest(StepOutputType.ElementPrint, variables.Select(v => v.ToString()));

        public static StepOutputRequest ElementPrintRaw(params string[] variables) =>
            new StepOutputRequest(StepOutputType.ElementPrint, variables);

        /// <summary>
        /// Creates an <c>*EL PRINT,ELSET=...</c> request.
        /// </summary>
        public static StepOutputRequest ElementPrint(string elementSetName, params ElementOutputVariable[] variables) =>
            new StepOutputRequest(StepOutputType.ElementPrint, variables.Select(v => v.ToString()), elementSetName);

        public static StepOutputRequest ElementPrintRaw(string elementSetName, params string[] variables) =>
            new StepOutputRequest(StepOutputType.ElementPrint, variables, elementSetName);
    }
}
