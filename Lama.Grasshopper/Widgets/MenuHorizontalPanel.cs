using GH_IO.Serialization;
using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Lama.Gh.Widgets
{
    /// <summary>
    /// Lays out child widgets in a single horizontal row (for compact DOF toggles).
    /// </summary>
    public class MenuHorizontalPanel : GH_Attr_Widget
    {
        private readonly List<GH_Attr_Widget> _controls = new List<GH_Attr_Widget>();
        public GH_Attr_Widget _activeControl;
        public GH_Capsule _menu;
        public float LeftMargin { get; set; } = 3f;
        public float RightMargin { get; set; } = 3f;
        public float TopMargin { get; set; } = 3f;
        public float BottomMargin { get; set; } = 3f;
        public int PanelRadius { get; set; } = 3;
        public int Space { get; set; } = 4;

        public void ClearActiveControl() => _activeControl = null;

        public MenuHorizontalPanel(int index, string id)
            : base(index, id)
        {
        }

        public void AddControl(GH_Attr_Widget control)
        {
            _controls.Add(control);
            control.Parent = this;
        }

        public override bool Write(GH_IWriter writer)
        {
            GH_IWriter writer2 = writer.CreateChunk("HPanel", Index);
            for (var i = 0; i < _controls.Count; i++)
                _controls[i].Write(writer2);
            return base.Write(writer);
        }

        public override bool Read(GH_IReader reader)
        {
            var reader2 = reader.FindChunk("HPanel", Index);
            if (reader2 != null)
            {
                for (var i = 0; i < _controls.Count; i++)
                    _controls[i].Read(reader2);
            }
            return base.Read(reader);
        }

        public override SizeF ComputeMinSize()
        {
            float rowW = 0f;
            float rowH = 0f;
            for (var i = 0; i < _controls.Count; i++)
            {
                var sz = _controls[i].ComputeMinSize();
                rowW += sz.Width;
                if (i > 0)
                    rowW += Space;
                rowH = Math.Max(rowH, sz.Height);
            }
            return new SizeF(
                LeftMargin + RightMargin + PanelRadius * 2 + rowW,
                TopMargin + BottomMargin + PanelRadius * 2 + rowH);
        }

        public override void PostUpdateBounds(out float outHeight)
        {
            outHeight = ComputeMinSize().Height;
        }

        public override void Layout()
        {
            float rowH = 0f;
            var minSizes = new List<SizeF>(_controls.Count);
            foreach (var c in _controls)
            {
                var sz = c.ComputeMinSize();
                minSizes.Add(sz);
                rowH = Math.Max(rowH, sz.Height);
            }

            float innerLeft = CanvasPivot.X + LeftMargin + PanelRadius;
            float innerW = CanvasBounds.Width - LeftMargin - RightMargin - PanelRadius * 2f;
            float y = CanvasPivot.Y + TopMargin + PanelRadius;

            float minTotalW = 0f;
            for (var i = 0; i < _controls.Count; i++)
            {
                minTotalW += minSizes[i].Width;
                if (i > 0)
                    minTotalW += Space;
            }

            if (_controls.Count > 0 && innerW >= minTotalW)
            {
                float colW = innerW / _controls.Count;
                for (var i = 0; i < _controls.Count; i++)
                {
                    var sz = minSizes[i];
                    float segmentLeft = innerLeft + i * colW;
                    float x = segmentLeft + (colW - sz.Width) / 2f;
                    float yPad = y + (rowH - sz.Height) / 2f;
                    var c = _controls[i];
                    c.UpdateBounds(new PointF(x, yPad), sz.Width);
                    c.Style = Style;
                    c.Palette = Palette;
                    c.Layout();
                }
            }
            else
            {
                float x = innerLeft;
                for (var i = 0; i < _controls.Count; i++)
                {
                    var sz = minSizes[i];
                    float yPad = y + (rowH - sz.Height) / 2f;
                    var c = _controls[i];
                    c.UpdateBounds(new PointF(x, yPad), sz.Width);
                    c.Style = Style;
                    c.Palette = Palette;
                    c.Layout();
                    x += sz.Width + Space;
                }
            }

            var rect = GH_Attr_Widget.Shrink(CanvasBounds, LeftMargin, RightMargin, TopMargin, BottomMargin);
            _menu = GH_Capsule.CreateTextCapsule(rect, rect, Palette, "", new Font(FontFamily.GenericSansSerif, 8f, FontStyle.Bold), 0, PanelRadius, 8);
        }

        public override void Render(WidgetRenderArgs args)
        {
            var canvas = args.Canvas;
            float zoom = canvas.Viewport.Zoom;
            int num = 255;
            if (zoom < 1f)
            {
                float z = (zoom - 0.5f) * 2f;
                num = (int)(num * z);
            }
            num = Math.Max(0, Math.Min(255, num));
            num = GH_Canvas.ZoomFadeLow;
            int r = Style.Fill.R, g = Style.Fill.G, b = Style.Fill.B;
            var val = new GH_PaletteStyle(Color.FromArgb(num, r, g, b), Color.FromArgb(num, 80, 80, 80));
            _menu?.Render(canvas.Graphics, val);
            foreach (var c in _controls)
                c.OnRender(args);
        }

        public override GH_ObjectResponse RespondToMouseUp(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            if (_activeControl != null)
            {
                var val = _activeControl.RespondToMouseUp(sender, e);
                if ((int)val == 2)
                {
                    _activeControl = null;
                    return val;
                }
                if ((int)val != 0)
                    return val;
                _activeControl = null;
            }
            return 0;
        }

        public override GH_ObjectResponse RespondToMouseDown(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            if (CanvasBounds.Contains(e.CanvasLocation))
            {
                foreach (var c in _controls)
                {
                    if (c.Contains(e.CanvasLocation) && c.Enabled)
                    {
                        var val = c.RespondToMouseDown(sender, e);
                        if ((int)val != 0)
                        {
                            _activeControl = c;
                            return val;
                        }
                    }
                }
            }
            else if (_activeControl != null)
            {
                _activeControl.RespondToMouseDown(sender, e);
                _activeControl = null;
                return GH_ObjectResponse.Handled;
            }
            return 0;
        }

        public override GH_ObjectResponse RespondToMouseMove(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            return _activeControl != null ? _activeControl.RespondToMouseMove(sender, e) : 0;
        }

        public override GH_ObjectResponse RespondToMouseDoubleClick(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            if (!CanvasBounds.Contains(e.CanvasLocation))
                return 0;
            foreach (var c in _controls)
            {
                if (c.Contains(e.CanvasLocation) && c.Enabled)
                    return c.RespondToMouseDoubleClick(sender, e);
            }
            return 0;
        }

        public override GH_Attr_Widget IsTtipPoint(PointF pt)
        {
            if (!CanvasBounds.Contains(pt))
                return null;
            foreach (var c in _controls)
            {
                var w = c.IsTtipPoint(pt);
                if (w != null)
                    return w;
            }
            return _showToolTip ? this : null;
        }
    }
}
