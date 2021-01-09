﻿/*
 * Copyright (C) 2013 FooProject
 * * This program is free software; you can redistribute it and/or modify it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 3 of the License, or (at your option) any later version.

 * This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of 
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.

You should have received a copy of the GNU General Public License along with this program. If not, see <http://www.gnu.org/licenses/>.
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace FooEditEngine
{
    /// <summary>
    /// LineBreakMethod列挙体
    /// </summary>
    public enum LineBreakMethod
    {
        /// <summary>
        /// 折り返さない
        /// </summary>
        None = 0,
        /// <summary>
        /// 右端で折り返す
        /// </summary>
        PageBound = 1,
        /// <summary>
        /// 文字数で折り返す
        /// </summary>
        CharUnit = 2
    }

    /// <summary>
    /// 余白を表す
    /// </summary>
    public struct Padding
    {
        /// <summary>
        /// 左余白
        /// </summary>
        public int Left;
        /// <summary>
        /// 上余白
        /// </summary>
        public int Top;
        /// <summary>
        /// 右余白
        /// </summary>
        public int Right;
        /// <summary>
        /// 下余白
        /// </summary>
        public int Bottom;
        /// <summary>
        /// コンストラクター
        /// </summary>
        /// <param name="left">左</param>
        /// <param name="top">上</param>
        /// <param name="right">右</param>
        /// <param name="bottom">下</param>
        public Padding(int left, int top, int right, int bottom)
        {
            this.Left = left;
            this.Top = top;
            this.Right = right;
            this.Bottom = bottom;
        }
    }

    abstract class ViewBase : IDisposable
    {
        const int SpiltCharCount = 1024;

        Document _Document;
        protected Rectangle _Rect;
        protected double _LongestWidth,_LongestHeight;
        Padding _Padding;

        public ViewBase(Document doc, ITextRender r,Padding padding)
        {
            this._Padding = padding;
            this.render = r;
            this.Document = doc;
            this.render.ChangedRenderResource += new ChangedRenderResourceEventHandler(render_ChangedRenderResource);
            this.render.ChangedRightToLeft += render_ChangedRightToLeft;
            this.SrcChanged += new EventHandler((s, e) => { });
            this.PageBoundChanged += new EventHandler((s, e) => { });
        }

        public Document Document
        {
            get
            {
                return this._Document;
            }
            set
            {
                if(this._Document != null)
                {
                    this._Document.Update -= new DocumentUpdateEventHandler(doc_Update);
                    this._Document.LineBreakChanged -= Document_LineBreakChanged;
                    this._Document.StatusUpdate -= Document_StatusUpdate;
                    this._Document.PerformLayouted -= _Document_PerformLayouted;
                }

                this._Document = value;

                this._Document.Update += new DocumentUpdateEventHandler(doc_Update);
                this._Document.LineBreakChanged += Document_LineBreakChanged;
                this._Document.StatusUpdate += Document_StatusUpdate;
                this._Document.PerformLayouted += _Document_PerformLayouted;

                this.Document_LineBreakChanged(this, null);

                this.Document_StatusUpdate(this, null);
            }
        }

        private void _Document_PerformLayouted(object sender, EventArgs e)
        {
            CalculateLineCountOnScreen();
        }

        private void Document_StatusUpdate(object sender, EventArgs e)
        {
            if (this.render == null)
                return;
            if (this.render.TabWidthChar != this.Document.TabStops)
                this.render.TabWidthChar = this.Document.TabStops;
            if (this.render.RightToLeft != this.Document.RightToLeft)
                this.render.RightToLeft = this.Document.RightToLeft;
            if (this.render.ShowFullSpace != this.Document.ShowFullSpace)
                this.render.ShowFullSpace = this.Document.ShowFullSpace;
            if (this.render.ShowHalfSpace != this.Document.ShowHalfSpace)
                this.render.ShowHalfSpace = this.Document.ShowHalfSpace;
            if (this.render.ShowTab != this.Document.ShowTab)
                this.render.ShowTab = this.Document.ShowTab;
            if (this.render.ShowLineBreak != this.Document.ShowLineBreak)
                this.render.ShowLineBreak = this.Document.ShowLineBreak;
            CalculateClipRect();
            CalculateLineCountOnScreen();
            this._LayoutLines.ClearLayoutCache();
        }

        private void Document_LineBreakChanged(object sender, EventArgs e)
        {
            this.CalculateLineBreak();
        }

        protected LineToIndexTable _LayoutLines
        {
            get
            {
                return this.Document.LayoutLines;
            }
        }

        public event EventHandler SrcChanged;

        public event EventHandler PageBoundChanged;

        /// <summary>
        /// テキストレンダラ―
        /// </summary>
        public ITextRender render
        {
            get;
            set;
        }

        /// <summary>
        /// 一ページの高さに収まる行数を返す
        /// </summary>
        public int LineCountOnScreen
        {
            get;
            protected set;
        }
        
        /// <summary>
        /// 折り返し時の右マージン
        /// </summary>
        public double LineBreakingMarginWidth
        {
            get;
            protected set;
        }

        /// <summary>
        /// 保持しているレイアウト行
        /// </summary>
        public LineToIndexTable LayoutLines
        {
            get { return this._LayoutLines; }
        }

        /// <summary>
        /// 最も長い行の幅
        /// </summary>
        public double LongestWidth
        {
            get { return this._LongestWidth; }
        }

        public double LineNumberMargin
        {
            get
            {
                return this.render.emSize.Width;
            }
        }

        /// <summary>
        /// シンタックスハイライター
        /// </summary>
        /// <remarks>差し替えた場合、再構築する必要があります</remarks>
        public IHilighter Hilighter
        {
            get { return this._LayoutLines.Hilighter; }
            set { this._LayoutLines.Hilighter = value; this._LayoutLines.ClearLayoutCache(); }
        }

        /// <summary>
        /// 余白を表す
        /// </summary>
        public Padding Padding
        {
            get {
                return this._Padding;
            }
            set {
                this._Padding = value;
                CalculateClipRect();
                CalculateLineBreak();
                CalculateLineCountOnScreen();
                if (this.Document.RightToLeft)
                    this._LayoutLines.ClearLayoutCache();
                this.PageBoundChanged(this, null);
            }
        }

        /// <summary>
        /// ページ全体を表す領域
        /// </summary>
        public Rectangle PageBound
        {
            get { return this._Rect; }
            set
            {
                if (value.Width < 0 || value.Height < 0)
                    throw new ArgumentOutOfRangeException("");
                this._Rect = value;
                CalculateClipRect();
                CalculateLineBreak();
                CalculateLineCountOnScreen();
                if (this.Document.RightToLeft)
                    this._LayoutLines.ClearLayoutCache();
                this.PageBoundChanged(this, null);
            }
        }

        /// <summary>
        /// Draw()の対象となる領域の左上を表す
        /// </summary>
        public SrcPoint Src
        {
            get { return this.Document.Src; }
            set { this.Document.Src = value; }
        }

        public virtual void Draw(Rectangle updateRect, bool force = false)
        {
            return;
        }

        /// <summary>
        /// スクロールを試行する
        /// </summary>
        /// <param name="x">X座標</param>
        /// <param name="row">行</param>
        /// <param name="rel_y">各行の左上を0とするY座標</param>
        /// <returns>成功すれば偽、そうでなければ真</returns>
        public virtual bool TryScroll(double x, int row,double rel_y = 0)
        {
            if (row < 0)
                return true;
            if (row > this.LayoutLines.Count - 1)
                return true;
            this.Document.Src = new SrcPoint(x, row, rel_y);
            this.SrcChanged(this,null);
            return false;
        }

        /// <summary>
        /// スクロールを試行する
        /// </summary>
        /// <param name="offset_x">X方向の移動量</param>
        /// <param name="offset_y">Y方向の移動量</param>
        /// <returns>成功すれば偽、そうでなければ真</returns>
        public virtual bool TryScroll(double offset_x,double offset_y)
        {
            double x = this.Document.Src.X - offset_x;
            if (x < 0)
                return true;
            var t = GetNearstRowAndOffsetY(this.Document.Src.Row, -this.Document.Src.OffsetY + offset_y);
            if (t == null)
                return true;
            this.Document.Src = new SrcPoint(x, t.Item1, -t.Item2);
            return false;
        }

        /// <summary>
        /// srcRowを起点としてrect_heightが収まる行とオフセットYを求めます
        /// </summary>
        /// <param name="srcRow">起点となる行</param>
        /// <param name="rect_hight">Y方向のバウンディングボックス</param>
        /// <returns>失敗した場合、NULL。成功した場合、行とオフセットY</returns>
        public Tuple<int,double> GetNearstRowAndOffsetY(int srcRow, double rect_hight)
        {
            if (rect_hight > 0)
            {
                for (int i = srcRow; i < this.Document.LayoutLines.Count; i++)
                {
                    ITextLayout layout = this.Document.LayoutLines.GetLayout(i);

                    int lineHeadIndex = this.LayoutLines.GetIndexFromLineNumber(i);
                    int lineLength = this.LayoutLines.GetLengthFromLineNumber(i);
                    double layoutHeight = layout.Height;

                    if (this.LayoutLines.FoldingCollection.IsHidden(lineHeadIndex))
                        continue;

                    if (rect_hight == 0)
                        return new Tuple<int, double>(i, 0);

                    if (rect_hight - layoutHeight < 0)
                        return new Tuple<int, double>(i, rect_hight);

                    rect_hight -= layoutHeight;
                }
            }
            else if(rect_hight < 0)
            {
                for (int i = srcRow - 1; i >= 0; i--)
                {
                    ITextLayout layout = this.Document.LayoutLines.GetLayout(i);

                    int lineHeadIndex = this.LayoutLines.GetIndexFromLineNumber(i);
                    int lineLength = this.LayoutLines.GetLengthFromLineNumber(i);
                    double layoutHeight = layout.Height;

                    if (this.LayoutLines.FoldingCollection.IsHidden(lineHeadIndex))
                        continue;

                    if(rect_hight == 0)
                        return new Tuple<int, double>(i, 0);

                    if (rect_hight + layoutHeight >= 0)
                        return new Tuple<int, double>(i, layoutHeight + rect_hight);

                    rect_hight += layoutHeight;
                }
                return new Tuple<int, double>(0, 0);
            }
            return null;
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        public virtual void CalculateLineBreak()
        {
            if (this.Document.LineBreak == LineBreakMethod.PageBound)
                this._LayoutLines.WrapWidth = this.render.TextArea.Width - LineBreakingMarginWidth;  //余白を残さないと欠ける
            else if (this.Document.LineBreak == LineBreakMethod.CharUnit)
                this._LayoutLines.WrapWidth = this.render.emSize.Width * this.Document.LineBreakCharCount;
            else
                this._LayoutLines.WrapWidth = LineToIndexTable.NONE_BREAK_LINE;
        }

        public virtual void CalculateLineCountOnScreen()
        {
        }

        public virtual void CalculateWhloeViewPort()
        {
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                this._Document.Update -= new DocumentUpdateEventHandler(this.doc_Update);    //これをしないと複数のビューを作成した時に妙なエラーが発生する
                this._Document.LineBreakChanged -= Document_LineBreakChanged;
                this._Document.StatusUpdate -= Document_StatusUpdate;
                this._Document.PerformLayouted -= _Document_PerformLayouted;
            }
            this._LayoutLines.Clear();
        }

        protected virtual void CalculateClipRect()
        {
        }


        protected virtual void OnSrcChanged(EventArgs e)
        {
            EventHandler handler = this.SrcChanged;
            if (handler != null)
                this.SrcChanged(this, e);
        }

        protected virtual void OnPageBoundChanged(EventArgs e)
        {
            EventHandler handler = this.PageBoundChanged;
            if (handler != null)
                this.PageBoundChanged(this, e);
        }

        void render_ChangedRightToLeft(object sender, EventArgs e)
        {
            this.Document.Src = new SrcPoint(0, this.Document.Src.Row, this.Src.OffsetY);
        }

        void render_ChangedRenderResource(object sender, ChangedRenderRsourceEventArgs e)
        {
            this._LayoutLines.ClearLayoutCache();
            if (e.type == ResourceType.Font)
            {
                this.CalculateClipRect();
                this.CalculateLineCountOnScreen();
                this.CalculateWhloeViewPort();
            }
        }

        void doc_Update(object sender, DocumentUpdateEventArgs e)
        {
            switch (e.type)
            {
                case UpdateType.RebuildLayout:
                case UpdateType.Clear:
                    this._LongestWidth = 0;
                    break;
            }
        }
    }
}
