/*
 * Copyright (C) 2013 FooProject
 * * This program is free software; you can redistribute it and/or modify it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 3 of the License, or (at your option) any later version.

 * This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of 
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.

You should have received a copy of the GNU General Public License along with this program. If not, see <http://www.gnu.org/licenses/>.
 */

#define CACHE_COLOR_BURSH

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FooEditEngine;
using SharpDX;
using D2D = SharpDX.Direct2D1;
using DW = SharpDX.DirectWrite;
using DXGI = SharpDX.DXGI;
using System.Runtime.InteropServices;

namespace FooEditEngine
{
    delegate void PreDrawOneLineHandler(MyTextLayout layout,LineToIndexTable lti,int row,double x,double y);

    delegate void GetDpiHandler(out float dpix,out float dpiy);

    /// <summary>
    /// 文字列のアンチエイリアシングモードを指定する
    /// </summary>
    public enum TextAntialiasMode
    {
        /// <summary>
        /// 最適なものが選択されます
        /// </summary>
        Default = D2D.TextAntialiasMode.Default,
        /// <summary>
        /// ClearTypeでアンチエイリアシングを行います
        /// </summary>
        ClearType = D2D.TextAntialiasMode.Cleartype,
        /// <summary>
        /// グレイスケールモードでアンチエイリアシングを行います
        /// </summary>
        GrayScale = D2D.TextAntialiasMode.Grayscale,
        /// <summary>
        /// アンチエイリアシングを行いません
        /// </summary>
        Aliased = D2D.TextAntialiasMode.Aliased,
    }

    sealed class ColorBrushCollection
    {
        ResourceManager<Color4, D2D.SolidColorBrush> cache = new ResourceManager<Color4, D2D.SolidColorBrush>();

        public D2D.SolidColorBrush Get(D2D.RenderTarget render,Color4 key)
        {
            D2D.SolidColorBrush brush;

#if CACHE_COLOR_BURSH
            bool result = cache.TryGetValue(key, out brush);
            if (!result)
            {
                brush = new D2D.SolidColorBrush(render, key);
                cache.Add(key, brush);
            }
#else
            brush = new D2D.SolidColorBrush(render, key);
#endif
            
            return brush;
        }

        public void Clear()
        {
            cache.Clear();
        }
    }

    sealed class StrokeCollection
    {
        ResourceManager<HilightType, D2D.StrokeStyle> cache = new ResourceManager<HilightType, D2D.StrokeStyle>();

        public D2D.StrokeStyle Get(D2D.RenderTarget render,HilightType type)
        {
            return this.Get(render.Factory, type);
        }

        [Obsolete]
        public D2D.StrokeStyle Get(HilightType type)
        {
            return this.Get(D2DRenderShared.D2DFactory, type);
        }

        public D2D.StrokeStyle Get(D2D.Factory factory,HilightType type)
        {
            D2D.StrokeStyle stroke;
            if (this.cache.TryGetValue(type, out stroke))
                return stroke;

            D2D.StrokeStyleProperties prop = new D2D.StrokeStyleProperties();
            prop.DashCap = D2D.CapStyle.Flat;
            prop.DashOffset = 0;
            prop.DashStyle = D2D.DashStyle.Solid;
            prop.EndCap = D2D.CapStyle.Flat;
            prop.LineJoin = D2D.LineJoin.Miter;
            prop.MiterLimit = 0;
            prop.StartCap = D2D.CapStyle.Flat;
            switch (type)
            {
                case HilightType.Sold:
                case HilightType.Url:
                case HilightType.Squiggle:
                    prop.DashStyle = D2D.DashStyle.Solid;
                    break;
                case HilightType.Dash:
                    prop.DashStyle = D2D.DashStyle.Dash;
                    break;
                case HilightType.DashDot:
                    prop.DashStyle = D2D.DashStyle.DashDot;
                    break;
                case HilightType.DashDotDot:
                    prop.DashStyle = D2D.DashStyle.DashDotDot;
                    break;
                case HilightType.Dot:
                    prop.DashStyle = D2D.DashStyle.Dot;
                    break;
            }
            stroke = new D2D.StrokeStyle(D2DRenderShared.D2DFactory, prop);
            this.cache.Add(type, stroke);
            return stroke;
        }
        public void Clear()
        {
            cache.Clear();
        }
    }

    class D2DRenderShared
    {
        static DW.Factory _DWFactory;
        static public DW.Factory DWFactory
        {
            get
            {
                if(_DWFactory == null)
                {
                    _DWFactory = new DW.Factory(DW.FactoryType.Shared);
                }
                return _DWFactory;
            }
        }
#if METRO || WINDOWS_UWP
        static D2D.Factory1 _D2DFactory;

        static public D2D.Factory1 D2DFactory
        {
            get
            {
                if (_D2DFactory == null)
                {
                    _D2DFactory = new D2D.Factory1(D2D.FactoryType.SingleThreaded);
                }
                return _D2DFactory;
            }
        }
#else
        static D2D.Factory _D2DFactory;
        static public D2D.Factory D2DFactory
        {
            get
            {
                if (_D2DFactory == null)
                {
                    _D2DFactory = new D2D.Factory(D2D.FactoryType.SingleThreaded);
                }
                return _D2DFactory;
            }
        }
#endif
    }

    class D2DRenderCommon : IDisposable
    {
        InlineManager HiddenChars;
        TextAntialiasMode _TextAntialiasMode;
        Color4 _ControlChar,_Forground,_URL,_Hilight;
        DW.TextFormat format;
        protected CustomTextRenderer textRender;
        protected D2D.Bitmap cachedBitMap;
        int tabLength = 8;
        bool _ShowLineBreak,_RightToLeft;
        Color4 _Comment, _Literal, _Keyword1, _Keyword2;
        protected bool hasCache = false;
        protected Size renderSize;

        protected ColorBrushCollection Brushes
        {
            get;
            private set;
        }

        protected StrokeCollection Strokes
        {
            get;
            private set;
        }

        D2D.RenderTarget _render;
        protected D2D.RenderTarget render
        {
            get { return _render; }
            set
            {
                _render = value;
                this.TextAntialiasMode = this._TextAntialiasMode;
                if(this.HiddenChars != null)
                    this.HiddenChars.ReGenerate();
            }
        }

        public D2DRenderCommon()
        {
            this.Brushes = new ColorBrushCollection();
            this.Strokes = new StrokeCollection();
            this.ChangedRenderResource += (s, e) => { };
            this.ChangedRightToLeft += (s, e) => { };
            this.renderSize = new Size();
        }

        public event ChangedRenderResourceEventHandler ChangedRenderResource;

        public event EventHandler ChangedRightToLeft;

        public const int MiniumeWidth = 40;    //これ以上ないと誤操作が起こる

        public void InitTextFormat(string fontName, float fontSize, DW.FontWeight fontWeigth = DW.FontWeight.Normal,DW.FontStyle fontStyle = DW.FontStyle.Normal)
        {
            if(this.format != null)
                this.format.Dispose();

            float dpix, dpiy;
            this.GetDpi(out dpix, out dpiy);

            float fontSizeInDIP = fontSize * (dpix / 72.0f);
            this.format = new DW.TextFormat(D2DRenderShared.DWFactory, fontName, fontWeigth, fontStyle, fontSizeInDIP);
            this.format.WordWrapping = DW.WordWrapping.NoWrap;
            this.format.ReadingDirection = GetDWRightDirect(_RightToLeft);

            if (this.HiddenChars == null)
                this.HiddenChars = new InlineManager(D2DRenderShared.DWFactory, this.format, this.ControlChar, this.Brushes);
            else
                this.HiddenChars.Format = this.format;

            this.TabWidthChar = this.TabWidthChar;

            this.hasCache = false;

            MyTextLayout layout = new MyTextLayout(D2DRenderShared.DWFactory, "0", this.format, float.MaxValue, float.MaxValue, dpix, false);
            layout.RightToLeft = false;
            this.emSize = new Size(layout.Width, layout.Height);
            layout.Dispose();

            layout = new MyTextLayout(D2DRenderShared.DWFactory, "+", this.format, float.MaxValue, float.MaxValue, dpix, false);
            layout.RightToLeft = false;
#if METRO
            this.FoldingWidth = Math.Max(D2DRenderCommon.MiniumeWidth, layout.Width);
#else
            this.FoldingWidth = layout.Width;
#endif
            layout.Dispose();

            this.OnChangedRenderResource(this,new ChangedRenderRsourceEventArgs(ResourceType.Font));
        }

        public void OnChangedRenderResource(object sender, ChangedRenderRsourceEventArgs e)
        {
            if (this.ChangedRenderResource != null)
                this.ChangedRenderResource(sender, e);
        }

        DW.ReadingDirection GetDWRightDirect(bool rtl)
        {
            return rtl ? DW.ReadingDirection.RightToLeft : DW.ReadingDirection.LeftToRight;
        }

        public bool RightToLeft
        {
            get
            {
                return _RightToLeft;
            }
            set
            {
                _RightToLeft = value;
                this.format.ReadingDirection = GetDWRightDirect(value);
                this.ChangedRightToLeft(this, null);                
            }
        }

        public TextAntialiasMode TextAntialiasMode
        {
            get
            {
                return this._TextAntialiasMode;
            }
            set
            {
                if (this.render == null)
                    throw new InvalidOperationException();
                this._TextAntialiasMode = value;
                this.render.TextAntialiasMode = (D2D.TextAntialiasMode)value;
                this.OnChangedRenderResource(this, new ChangedRenderRsourceEventArgs(ResourceType.Antialias));
            }
        }

        public bool ShowFullSpace
        {
            get
            {
                if (this.HiddenChars == null)
                    return false;
                else
                    return this.HiddenChars.ContainsSymbol('　');
            }
            set
            {
                if (this.HiddenChars == null)
                    throw new InvalidOperationException();
                if (value)
                    this.HiddenChars.AddSymbol('　', '□');
                else
                    this.HiddenChars.RemoveSymbol('　');
            }
        }

        public bool ShowHalfSpace
        {
            get
            {
                if (this.HiddenChars == null)
                    return false;
                else
                    return this.HiddenChars.ContainsSymbol(' ');
            }
            set
            {
                if (this.HiddenChars == null)
                    throw new InvalidOperationException();
                if (value)
                    this.HiddenChars.AddSymbol(' ', 'ﾛ');
                else
                    this.HiddenChars.RemoveSymbol(' ');
            }
        }

        public bool ShowTab
        {
            get
            {
                if (this.HiddenChars == null)
                    return false;
                else
                    return this.HiddenChars.ContainsSymbol('\t');
            }
            set
            {
                if (this.HiddenChars == null)
                    throw new InvalidOperationException();
                if (value)
                    this.HiddenChars.AddSymbol('\t', '>');
                else
                    this.HiddenChars.RemoveSymbol('\t');
            }
        }

        public bool ShowLineBreak
        {
            get
            {
                return this._ShowLineBreak;
            }
            set
            {
                this._ShowLineBreak = value;
            }
        }

        public Color4 Foreground
        {
            get
            {
                return this._Forground;
            }
            set
            {
                if (this.render == null)
                    return;
                this._Forground = value;
                if (this.textRender != null)
                    this.textRender.DefaultFore = value;
                this.OnChangedRenderResource(this, new ChangedRenderRsourceEventArgs(ResourceType.Brush));
            }
        }

        public Color4 HilightForeground
        {
            get;
            set;
        }

        public Color4 Background
        {
            get;
            set;
        }

        public Color4 InsertCaret
        {
            get;
            set;
        }

        public Color4 OverwriteCaret
        {
            get;
            set;
        }

        public Color4 LineMarker
        {
            get;
            set;
        }

        public Color4 UpdateArea
        {
            get;
            set;
        }

        public Color4 LineNumber
        {
            get;
            set;
        }

        public Color4 ControlChar
        {
            get
            {
                return this._ControlChar;
            }
            set
            {
                this._ControlChar = value;
                if (this.HiddenChars != null)
                    this.HiddenChars.Fore = value;
                this.OnChangedRenderResource(this, new ChangedRenderRsourceEventArgs(ResourceType.Brush));
            }
        }

        public Color4 Url
        {
            get
            {
                return this._URL;
            }
            set
            {
                this._URL = value;
                this.OnChangedRenderResource(this, new ChangedRenderRsourceEventArgs(ResourceType.Brush));
            }
        }

        public Color4 Hilight
        {
            get
            {
                return this._Hilight;
            }
            set
            {
                this._Hilight = value;
            }
        }

        public Color4 Comment
        {
            get
            {
                return this._Comment;
            }
            set
            {
                this._Comment = value;
                this.OnChangedRenderResource(this, new ChangedRenderRsourceEventArgs(ResourceType.Brush));
            }
        }

        public Color4 Literal
        {
            get
            {
                return this._Literal;
            }
            set
            {
                this._Literal = value;
                this.OnChangedRenderResource(this, new ChangedRenderRsourceEventArgs(ResourceType.Brush));
            }
        }

        public Color4 Keyword1
        {
            get
            {
                return this._Keyword1;
            }
            set
            {
                this._Keyword1 = value;
                this.OnChangedRenderResource(this, new ChangedRenderRsourceEventArgs(ResourceType.Brush));
            }
        }

        public Color4 Keyword2
        {
            get
            {
                return this._Keyword2;
            }
            set
            {
                this._Keyword2 = value;
                this.OnChangedRenderResource(this, new ChangedRenderRsourceEventArgs(ResourceType.Brush));
            }
        }

        public Rectangle TextArea
        {
            get;
            set;
        }

        public double LineNemberWidth
        {
            get
            {
                return this.emSize.Width * EditView.LineNumberLength;
            }
        }

        public double FoldingWidth
        {
            get;
            private set;
        }

        public int TabWidthChar
        {
            get { return this.tabLength; }
            set
            {
                if (value == 0)
                    return;
                this.tabLength = value;
                DW.TextLayout layout = new DW.TextLayout(D2DRenderShared.DWFactory, "0", this.format, float.MaxValue, float.MaxValue);
                float width = (float)(layout.Metrics.Width * value);
                this.HiddenChars.TabWidth = width;
                this.format.IncrementalTabStop = width;
                layout.Dispose();
            }
        }

        public Size emSize
        {
            get;
            private set;
        }

        public void DrawGripper(Point p, double radius)
        {
            D2D.Ellipse ellipse = new D2D.Ellipse();
            ellipse.Point = p;
            ellipse.RadiusX = (float)radius;
            ellipse.RadiusY = (float)radius;
            this.render.FillEllipse(ellipse, this.Brushes.Get(this.render,this.Background));
            this.render.DrawEllipse(ellipse, this.Brushes.Get(this.render, this.Foreground));
        }


        public virtual void DrawCachedBitmap(Rectangle rect)
        {
        }

        public virtual void CacheContent()
        {
        }

        public virtual bool IsVaildCache()
        {
            return this.hasCache;
        }

        protected void BegineDraw()
        {
            if (this.render == null || this.render.IsDisposed)
                return;
            this.render.BeginDraw();
            this.render.AntialiasMode = D2D.AntialiasMode.Aliased;
        }

        protected void EndDraw()
        {
            if (this.render == null || this.render.IsDisposed)
                return;
            this.render.AntialiasMode = D2D.AntialiasMode.PerPrimitive;
            this.render.EndDraw();
        }

        public void DrawString(string str, double x, double y, StringAlignment align, Size layoutRect, StringColorType colorType = StringColorType.Forground)
        {
            if (this.render == null || this.render.IsDisposed)
                return;
            float dpix, dpiy;
            D2D.SolidColorBrush brush;
            switch (colorType)
            {
                case StringColorType.Forground:
                    brush = this.Brushes.Get(this.render, this.Foreground);
                    break;
                case StringColorType.LineNumber:
                    brush = this.Brushes.Get(this.render, this.LineNumber);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            this.GetDpi(out dpix, out dpiy);
            MyTextLayout layout = new MyTextLayout(D2DRenderShared.DWFactory, str, this.format, (float)layoutRect.Width, (float)layoutRect.Height,dpix,false);
            layout.StringAlignment = align;
            layout.Draw(this.render, (float)x, (float)y, brush);
            layout.Dispose();
        }

        public void DrawFoldingMark(bool expand, double x, double y)
        {
            string mark = expand ? "-" : "+";
            this.DrawString(mark, x, y,StringAlignment.Left, new Size(this.FoldingWidth, this.emSize.Height));
        }

        public void FillRectangle(Rectangle rect,FillRectType type)
        {
            if (this.render == null || this.render.IsDisposed)
                return;
            D2D.SolidColorBrush brush = null;
            switch(type)
            {
                case FillRectType.OverwriteCaret:
                    brush = this.Brushes.Get(this.render, this.OverwriteCaret);
                    this.render.FillRectangle(rect, brush);
                    break;
                case FillRectType.InsertCaret:
                    brush = this.Brushes.Get(this.render, this.InsertCaret);
                    this.render.FillRectangle(rect, brush);
                    break;
                case FillRectType.InsertPoint:
                    brush = this.Brushes.Get(this.render, this.Hilight);
                    this.render.FillRectangle(rect, brush);
                    break;
                case FillRectType.LineMarker:
                    brush = this.Brushes.Get(this.render, this.LineMarker);
                    this.render.DrawRectangle(rect, brush, EditView.LineMarkerThickness);
                    break;
                case FillRectType.UpdateArea:
                    brush = this.Brushes.Get(this.render, this.UpdateArea);
                    this.render.FillRectangle(rect, brush);
                    break;
            }
        }

        public void FillBackground(Rectangle rect)
        {
            if (this.render == null || this.render.IsDisposed)
                return;
            this.render.Clear(this.Background);
        }

        public void DrawOneLine(Document doc,LineToIndexTable lti, int row, double x, double y, PreDrawOneLineHandler PreDrawOneLine)
        {
            int lineLength = lti.GetLengthFromLineNumber(row);

            if (lineLength == 0 || this.render == null || this.render.IsDisposed)
                return;

            MyTextLayout layout = (MyTextLayout)lti.GetLayout(row);

            if(PreDrawOneLine != null)
                PreDrawOneLine(layout,lti,row,x,y);

            if(doc.Selections.Count > 0)
            {
                int lineIndex = lti.GetIndexFromLineNumber(row);
                var SelectRanges = from s in doc.Selections.Get(lineIndex, lineLength)
                                   let n = Util.ConvertAbsIndexToRelIndex(s, lineIndex, lineLength)
                                   select n;

                if (SelectRanges != null)
                {
                    foreach (Selection sel in SelectRanges)
                    {
                        if (sel.length == 0 || sel.start == -1)
                            continue;

                        this.DrawMarkerEffect(layout, HilightType.Select, sel.start, sel.length, x, y, false);
                    }
                }
            }

            layout.Draw(this.render,textRender, (float)x, (float)y);

        }

        public void BeginClipRect(Rectangle rect)
        {
            this.render.PushAxisAlignedClip(rect, D2D.AntialiasMode.Aliased);
        }

        public void EndClipRect()
        {
            this.render.PopAxisAlignedClip();
        }

        public void SetTextColor(MyTextLayout layout,int start, int length, Color4? color)
        {
            if (color == null)
                return;
            layout.SetDrawingEffect(this.Brushes.Get(this.render, (Color4)color), new DW.TextRange(start, length));
        }

        public void DrawLine(Point from, Point to)
        {
            D2D.Brush brush = this.Brushes.Get(this.render, this.Foreground);
            D2D.StrokeStyle stroke = this.Strokes.Get(D2DRenderShared.D2DFactory,HilightType.Sold);
            this.render.DrawLine(from, to, brush, 1.0f, stroke);
        }

        public const int BoldThickness = 2;
        public const int NormalThickness = 1;

        public void DrawMarkerEffect(MyTextLayout layout, HilightType type, int start, int length, double x, double y, bool isBold, Color4? effectColor = null)
        {
            if (type == HilightType.None)
                return;

            float thickness = isBold ? BoldThickness : NormalThickness;

            Color4 color;
            if (effectColor != null)
                color = (Color4)effectColor;
            else if (type == HilightType.Select)
                color = this.Hilight;
            else
                color = this.Foreground;

            IMarkerEffecter effecter = null;
            D2D.SolidColorBrush brush = this.Brushes.Get(this.render, color);

            if (type == HilightType.Squiggle)
                effecter = new D2DSquilleLineMarker(this.render, brush, this.Strokes.Get(D2DRenderShared.D2DFactory, HilightType.Squiggle), thickness);
            else if (type == HilightType.Select)
                effecter = new HilightMarker(this.render, brush);
            else if (type == HilightType.None)
                effecter = null;
            else
                effecter = new LineMarker(this.render, brush, this.Strokes.Get(D2DRenderShared.D2DFactory,type), thickness);

            if (effecter != null)
            {
                bool isUnderline = type != HilightType.Select;

                DW.HitTestMetrics[] metrics = layout.HitTestTextRange(start, length, (float)x, (float)y);
                foreach (DW.HitTestMetrics metric in metrics)
                {
                    float offset = isUnderline ? metric.Height : 0;
                    effecter.Draw(metric.Left, metric.Top + offset, metric.Width, metric.Height);
                }
            }
        }

        public ITextLayout CreateLaytout(string str, SyntaxInfo[] syntaxCollection, IEnumerable<Marker> MarkerRanges, IEnumerable<Selection> SelectRanges,double WrapWidth)
        {
            float dpix,dpiy;
            this.GetDpi(out dpix,out dpiy);

            double layoutWidth = this.TextArea.Width;
            if (WrapWidth != LineToIndexTable.NONE_BREAK_LINE)
            {
                this.format.WordWrapping = DW.WordWrapping.Wrap;
                layoutWidth = WrapWidth;
            }
            else
            {
                this.format.WordWrapping = DW.WordWrapping.NoWrap;
            }

            bool hasNewLine = str.Length > 0 && str[str.Length - 1] == Document.NewLine;
            MyTextLayout newLayout = new MyTextLayout(D2DRenderShared.DWFactory,
                str,
                this.format,
                layoutWidth,
                this.TextArea.Height,
                dpiy,
                hasNewLine && this._ShowLineBreak);
            ParseLayout(newLayout, str);
            if (syntaxCollection != null)
            {
                foreach (SyntaxInfo s in syntaxCollection)
                {
                    D2D.SolidColorBrush brush = this.Brushes.Get(this.render, this.Foreground);
                    switch (s.type)
                    {
                        case TokenType.Comment:
                            brush = this.Brushes.Get(this.render, this.Comment);
                            break;
                        case TokenType.Keyword1:
                            brush = this.Brushes.Get(this.render, this.Keyword1);
                            break;
                        case TokenType.Keyword2:
                            brush = this.Brushes.Get(this.render, this.Keyword2);
                            break;
                        case TokenType.Literal:
                            brush = this.Brushes.Get(this.render, this.Literal);
                            break;
                    }
                    newLayout.SetDrawingEffect(brush, new DW.TextRange(s.index, s.length));
                }
            }

            if (MarkerRanges != null)
            {
                foreach (Marker m in MarkerRanges)
                {
                    if (m.start == -1 || m.length == 0)
                        continue;
                    Color4 color = new Color4(m.color.R / 255.0f, m.color.G / 255.0f, m.color.B / 255.0f, m.color.A / 255.0f);
                    if (m.hilight == HilightType.Url)
                        color = this.Url;
                    if (m.hilight == HilightType.Select)
                        newLayout.SetDrawingEffect(new SelectedEffect(this.HilightForeground, color, m.isBoldLine), new DW.TextRange(m.start, m.length));
                    else
                        newLayout.SetDrawingEffect(new DrawingEffect(m.hilight, color,m.isBoldLine), new DW.TextRange(m.start, m.length));
                    if (m.hilight != HilightType.None && m.hilight != HilightType.Select)
                        newLayout.SetUnderline(true, new DW.TextRange(m.start, m.length));
                }
            }

            if (SelectRanges != null && this.HilightForeground.Alpha > 0.0)
            {
                foreach (Selection sel in SelectRanges)
                {
                    if (sel.length == 0 || sel.start == -1)
                        continue;

                    newLayout.SetDrawingEffect(new SelectedEffect(this.HilightForeground, this.Hilight, false), new DW.TextRange(sel.start, sel.length));
                }
            }

            this.format.WordWrapping = DW.WordWrapping.NoWrap;

            return newLayout;
       }

        bool _Disposed = false;
        public void Dispose()
        {
            if (!_Disposed)
            {
                this.Dispose(true);
                this.HiddenChars.Clear();
                if (this.format != null)
                    this.format.Dispose();
            }
            this._Disposed = true;
        }

        protected virtual void Dispose(bool dispose)
        {
        }

        public virtual void GetDpi(out float dpix,out float dpiy)
        {
            throw new NotImplementedException();
        }

        public double GetScale()
        {
            float dpi;
            this.GetDpi(out dpi, out dpi);
            return dpi / 96.0;
        }

        void ParseLayout(MyTextLayout layout, string str)
        {
            for (int i = 0; i < str.Length; i++)
            {
                DW.InlineObject inlineObject = this.HiddenChars.Get(layout,i, str);
                if (inlineObject != null)
                {
                    layout.SetInlineObject(inlineObject, new DW.TextRange(i, 1));
                    layout.SetDrawingEffect(this.Brushes.Get(this.render, this.ControlChar), new DW.TextRange(i, 1));
                }
            }
            layout.SetLineBreakBrush(this.Brushes.Get(this.render, this.ControlChar));
        }
    }
}
