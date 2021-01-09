/*
 * Copyright (C) 2013 FooProject
 * * This program is free software; you can redistribute it and/or modify it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 3 of the License, or (at your option) any later version.

 * This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of 
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.

You should have received a copy of the GNU General Public License along with this program. If not, see <http://www.gnu.org/licenses/>.
 */
using System;
using SharpDX;
using D2D = SharpDX.Direct2D1;
using DW = SharpDX.DirectWrite;

namespace FooEditEngine
{
    sealed class InlineChar : DW.InlineObject
    {
        DW.TextLayout Layout;
        ColorBrushCollection brushes;
        Color4 DefaultFore;
        public InlineChar(DW.Factory factory, DW.TextFormat format, ColorBrushCollection brushes, Color4 Fore, char c)
        {
            this.Layout = new DW.TextLayout(factory, c.ToString(), format, float.MaxValue, float.MaxValue);
            this.Layout.ReadingDirection = DW.ReadingDirection.LeftToRight;
            this.AlternativeChar = c;
            this.brushes = brushes;
            this.DefaultFore = Fore;
        }

        public char AlternativeChar
        {
            get;
            private set;
        }

        public void Draw(object clientDrawingContext, DW.TextRenderer renderer, float originX, float originY, bool isSideways, bool isRightToLeft, ComObject clientDrawingEffect)
        {
            D2D.RenderTarget render = clientDrawingContext as D2D.RenderTarget;
            if (render == null)
                return;

            D2D.SolidColorBrush foreBrush = this.brushes.Get(render,this.DefaultFore);
            if (clientDrawingEffect != null)
            {
                var drawingForeBrush = clientDrawingEffect as D2D.SolidColorBrush;
                var selectedEffect = clientDrawingEffect as SelectedEffect;

                if (drawingForeBrush != null)
                {
                    foreBrush = drawingForeBrush;
                }
                else if (selectedEffect != null)
                {
                    foreBrush = this.brushes.Get(render, selectedEffect.Fore);
                }
            }

            render.DrawTextLayout(
                new Vector2(originX, originY),
                this.Layout,
                foreBrush);
        }

        public void GetBreakConditions(out DW.BreakCondition breakConditionBefore, out DW.BreakCondition breakConditionAfter)
        {
            breakConditionAfter = DW.BreakCondition.CanBreak;
            breakConditionBefore = DW.BreakCondition.CanBreak;
        }

        DW.InlineObjectMetrics? _Metrics;
        public DW.InlineObjectMetrics Metrics
        {
            get
            {
                if (_Metrics == null)
                {
                    DW.InlineObjectMetrics value = new DW.InlineObjectMetrics();
                    value.Height = this.Layout.Metrics.Height;
                    value.Width = this.Layout.Metrics.Width;
                    DW.LineMetrics[] lines = this.Layout.GetLineMetrics();
                    value.Baseline = lines[0].Baseline;
                    _Metrics = value;
                }
                return _Metrics.Value;
            }
        }

        public DW.OverhangMetrics? _OverhangMetrics;
        public DW.OverhangMetrics OverhangMetrics
        {
            get
            {
                if (_OverhangMetrics == null)
                {
                    DW.OverhangMetrics value = new DW.OverhangMetrics();
                    DW.TextMetrics metrics = this.Layout.Metrics;
                    value.Left = metrics.Left;
                    value.Right = metrics.Left + metrics.Width;
                    value.Top = metrics.Top;
                    value.Bottom = metrics.Top + metrics.Height;
                    _OverhangMetrics = value;
                }
                return _OverhangMetrics.Value;
            }
        }

        public IDisposable Shadow
        {
            get;
            set;
        }

        public void Dispose()
        {
            this.Layout.Dispose();
            return;
        }
    }

    sealed class InlineTab : DW.InlineObject
    {
        double _TabWidth;
        double LineHeight;
        ColorBrushCollection brushes;
        Color4 DefaultFore;
        public InlineTab(ColorBrushCollection brushes, Color4 Fore, double witdh, double lineHeight)
        {
            this._TabWidth = witdh;
            this.LineHeight = lineHeight;
            this.brushes = brushes;
            this.DefaultFore = Fore;
        }

        DW.InlineObjectMetrics? _Metrics;
        public DW.InlineObjectMetrics Metrics
        {
            get
            {
                if (_Metrics == null)
                {
                    DW.InlineObjectMetrics value = new DW.InlineObjectMetrics();
                    value.Width = (float)this._TabWidth;
                    value.Height = (float)LineHeight;
                    value.Baseline = (float)(value.Height * 0.8);    //デフォルトでは８割らしい
                    value.SupportsSideways = false;
                    _Metrics = value;
                }
                return _Metrics.Value;
            }
        }
        public DW.OverhangMetrics? _OverhangMetrics;
        public DW.OverhangMetrics OverhangMetrics
        {
            get
            {
                if (_OverhangMetrics == null)
                {
                    DW.OverhangMetrics value = new DW.OverhangMetrics();
                    DW.InlineObjectMetrics metrics = this.Metrics;
                    value.Left = 0;
                    value.Right = metrics.Width;
                    value.Top = 0;
                    value.Bottom = metrics.Height;
                    _OverhangMetrics = value;
                }
                return _OverhangMetrics.Value;
            }
        }

        public void Draw(object clientDrawingContext, DW.TextRenderer renderer, float originX, float originY, bool isSideways, bool isRightToLeft, ComObject clientDrawingEffect)
        {
            D2D.RenderTarget render = clientDrawingContext as D2D.RenderTarget;
            if (render == null)
                return;
            D2D.SolidColorBrush foreBrush = this.brushes.Get(render, this.DefaultFore);
            if (clientDrawingEffect != null)
            {
                var drawingForeBrush = clientDrawingEffect as D2D.SolidColorBrush;
                var selectedEffect = clientDrawingEffect as SelectedEffect;

                if (drawingForeBrush != null)
                {
                    foreBrush = drawingForeBrush;
                }
                else if (selectedEffect != null)
                {
                    foreBrush = this.brushes.Get(render, selectedEffect.Fore);
                }
            }
            DW.InlineObjectMetrics metrics = this.Metrics;
            float width = metrics.Width - 1;
            if (isRightToLeft)
            {
                originX += 1;
                render.DrawLine(new Vector2(originX, originY + metrics.Baseline), new Vector2(originX + width, originY + metrics.Baseline), foreBrush);
                render.DrawLine(new Vector2(originX, originY + metrics.Baseline / 2), new Vector2(originX, originY + metrics.Baseline), foreBrush);
            }
            else
            {
                render.DrawLine(new Vector2(originX, originY + metrics.Baseline), new Vector2(originX + width, originY + metrics.Baseline), foreBrush);
                render.DrawLine(new Vector2(originX + width, originY + metrics.Baseline / 2), new Vector2(originX + width, originY + metrics.Baseline), foreBrush);
            }
        }

        public void GetBreakConditions(out DW.BreakCondition breakConditionBefore, out DW.BreakCondition breakConditionAfter)
        {
            breakConditionAfter = DW.BreakCondition.CanBreak;
            breakConditionBefore = DW.BreakCondition.CanBreak;
        }

        public IDisposable Shadow
        {
            get;
            set;
        }

        public void Dispose()
        {
        }
    }
}
