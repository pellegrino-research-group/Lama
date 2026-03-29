using Grasshopper.Kernel;
using Grasshopper.Kernel.Special;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;


namespace Lama.Gh.Widgets
{
    public static class ValueList
    {
        /// <summary>
        /// Ensures a GH_ValueList exists on a given input index, creating and wiring one if needed,
        /// then populates it with the provided names/values.
        /// </summary>
        public static GH_ValueList EnsureValueList(
            GH_Component component,
            int inputIndex,
            IReadOnlyList<string> names,
            IReadOnlyList<int> values = null,
            GH_ValueListMode listMode = GH_ValueListMode.DropDown,
            int toggleIndex = 0,
            bool createIfMissing = true)
        {
            if (component == null)
                throw new ArgumentNullException(nameof(component));
            if (names == null || names.Count == 0)
                throw new ArgumentException("Names cannot be null or empty.", nameof(names));
            if (inputIndex < 0 || inputIndex >= component.Params.Input.Count)
                throw new ArgumentOutOfRangeException(nameof(inputIndex), "Input index is out of range.");

            var targetInput = component.Params.Input[inputIndex];
            var valueList = targetInput.Sources.OfType<GH_ValueList>().FirstOrDefault();

            if (valueList == null && createIfMissing)
            {
                var doc = component.OnPingDocument();
                if (doc == null)
                    return null;

                valueList = new GH_ValueList();
                valueList.CreateAttributes();

                var inputPivot = targetInput.Attributes?.Pivot ?? component.Attributes?.Pivot ?? new PointF(0, 0);
                valueList.Attributes.Pivot = new PointF(inputPivot.X - 220f, inputPivot.Y - 10f);

                doc.AddObject(valueList, false);
                targetInput.AddSource(valueList);
            }

            if (valueList == null)
                return null;

            var hasExplicitValues = values != null && values.Count == names.Count;
            var expectedExpressions = names
                .Select((n, i) => hasExplicitValues ? values[i].ToString() : $"\"{n}\"")
                .ToList();

            var currentNames = valueList.ListItems.Select(x => x.Name).ToList();
            var currentExpressions = valueList.ListItems.Select(x => x.Expression).ToList();
            var requiresUpdate = !currentNames.SequenceEqual(names) || !currentExpressions.SequenceEqual(expectedExpressions);

            if (requiresUpdate)
            {
                valueList.ListItems.Clear();
                for (var i = 0; i < names.Count; i++)
                {
                    valueList.ListItems.Add(new GH_ValueListItem(names[i], expectedExpressions[i]));
                }
            }

            valueList.ListMode = listMode;
            if (valueList.ListItems.Count > 0)
            {
                var clampedIndex = Math.Max(0, Math.Min(toggleIndex, valueList.ListItems.Count - 1));
                valueList.SelectItem(clampedIndex);
            }

            if (requiresUpdate)
                valueList.ExpireSolution(true);

            return valueList;
        }

        public static void UpdateValueLists(GH_Component component, int inputIndex, List<string> names, List<int> values = null, GH_ValueListMode listMode = GH_ValueListMode.DropDown, int toggleIndex = 1)
        {
            if (component == null || names == null || names.Count == 0)
            {
                return;
            }

            foreach (var source in component.Params.Input[inputIndex].Sources.OfType<GH_ValueList>())
            {
                if (!names.SequenceEqual(source.ListItems.Select(x => x.Name)))
                {
                    source.ListItems.Clear();
                    int num = 0;

                    if (values == null || values.Count != names.Count)
                    {
                        foreach (var name in names)
                        {
                            source.ListItems.Add(new GH_ValueListItem(name, $"\"{name}\""));
                        }
                    }
                    else
                    {
                        foreach (var name in names)
                        {
                            source.ListItems.Add(new GH_ValueListItem(name, values[num++].ToString()));
                        }
                    }

                    source.ListMode = listMode;
                    if (source.ListItems.Count > 0)
                    {
                        var clampedIndex = Math.Max(0, Math.Min(toggleIndex, source.ListItems.Count - 1));
                        source.SelectItem(clampedIndex);
                    }
                    source.ExpireSolution(true);
                }
            }
        }
    }
}