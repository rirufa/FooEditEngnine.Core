/*
 * Copyright (C) 2013 FooProject
 * * This program is free software; you can redistribute it and/or modify it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 3 of the License, or (at your option) any later version.

 * This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of 
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.

You should have received a copy of the GNU General Public License along with this program. If not, see <http://www.gnu.org/licenses/>.
 */
using System;
using System.Collections.Generic;
using SharpDX;
using D2D = SharpDX.Direct2D1;
using DW = SharpDX.DirectWrite;

namespace FooEditEngine
{
    sealed class InlineManager
    {
        DW.Factory Factory;
        DW.TextFormat _Format;
        Color4 _Fore;
        MultiSet<char, InlineChar> InlineChars = new MultiSet<char, InlineChar>();
        MultiSet<double, InlineTab> InlineTabs = null;
        ColorBrushCollection Brushes;
        double _TabWidth;
        const int DuplicateCount = 2;   //1だとDirectWriteが一つにまとめてしまう

        public InlineManager(DW.Factory factory, DW.TextFormat format, Color4 fore, ColorBrushCollection brushes, Size emSize)
        {
            this.Factory = factory;
            this._Format = format;
            this._Fore = fore;
            this.Brushes = brushes;
            this.emSize = emSize;
        }

        public bool ContainsSymbol(char c)
        {
            if (c == '\t')
                return this.InlineTabs != null && this.InlineTabs.Count > 0;
            return this.InlineChars.ContainsKey(c);
        }

        public void AddSymbol(char c, char alt)
        {
            if (c == '\t')
            {
                this.InlineTabs = new MultiSet<double, InlineTab>();
            }
            else
            {
                for (int i = 0; i < DuplicateCount; i++)
                {
                    this.InlineChars.Add(c, new InlineChar(this.Factory, this.Format, this.Brushes, this.Fore, alt));
                }
            }
        }

        public void RemoveSymbol(char c)
        {
            if (c == '\t')
                this.InlineTabs = null;
            else
                this.InlineChars.Remove(c);
        }

        public Color4 Fore
        {
            get
            {
                return this._Fore;
            }
            set
            {
                this._Fore = value;
                this.Format = this._Format;
            }
        }

        public Size emSize
        {
            get;
            set;
        }

        public DW.TextFormat Format
        {
            get
            {
                return this._Format;
            }
            set
            {
                this._Format = value;

                this.TabWidth = this._TabWidth;

                this.ReGenerate();
            }
        }

        public double TabWidth
        {
            get
            {
                return this._TabWidth;
            }
            set
            {
                this._TabWidth = value;
                if (this.InlineTabs != null)
                    this.InlineTabs.Clear();
            }
        }

        public DW.InlineObject Get(MyTextLayout layout, int index, string str)
        {
            if (str[index] == '\t')
            {
                if (this.InlineTabs == null)
                    return null;
                double x = layout.GetColPostionFromIndex(index);
                if (layout.RightToLeft)
                    x = layout.MaxWidth - x;
                double width;
                if (index > 0 && str[index - 1] == '\t')
                    width = this._TabWidth;
                else
                    width = this._TabWidth - x % this._TabWidth;

                List<InlineTab> collection;
                if (!this.InlineTabs.TryGet(width, out collection))
                {
                    collection = new List<InlineTab>();
                    for (int i = 0; i < DuplicateCount; i++)
                        collection.Add(new InlineTab(this.Brushes, this.Fore, width, this.emSize.Height));
                    this.InlineTabs.Add(width, collection);
                }
                return collection[index % DuplicateCount];
            }
            else
            {
                char c = str[index];
                if (this.InlineChars.ContainsKey(c) == false)
                    return null;
                return this.InlineChars.Get(c, index % DuplicateCount);
            }
        }

        public void Clear()
        {
            if (this.InlineChars != null)
                this.InlineChars.Clear();
            if (this.InlineTabs != null)
                this.InlineTabs.Clear();
        }

        public void ReGenerate()
        {
            List<KeyValuePair<char, char>> list = new List<KeyValuePair<char, char>>(this.InlineChars.Count);
            foreach (KeyValuePair<char, InlineChar> kv in this.InlineChars.EnumrateKeyAndFirstValue())
                list.Add(new KeyValuePair<char, char>(kv.Key, kv.Value.AlternativeChar));

            this.InlineChars.Clear();

            if (this.InlineTabs != null)
                this.InlineTabs.Clear();

            foreach (KeyValuePair<char, char> kv in list)
                for (int i = 0; i < DuplicateCount; i++)
                    this.InlineChars.Add(kv.Key, new InlineChar(this.Factory, this.Format, this.Brushes, this.Fore, kv.Value));
        }

    }
    sealed class MultiSet<T, J>
        where J : IDisposable
    {
        Dictionary<T, List<J>> Collection = new Dictionary<T, List<J>>();

        public void Add(T key, List<J> collection)
        {
            if (this.Collection.ContainsKey(key) == false)
                this.Collection.Add(key, collection);
        }

        public void Add(T key, J value)
        {
            if (this.Collection.ContainsKey(key) == false)
                this.Collection.Add(key, new List<J>());
            this.Collection[key].Add(value);
        }

        public int Count
        {
            get
            {
                return this.Collection.Count;
            }
        }

        public bool ContainsKey(T key)
        {
            return this.Collection.ContainsKey(key);
        }

        public bool TryGet(T key, out List<J> value)
        {
            return this.Collection.TryGetValue(key, out value);
        }

        public J Get(T key, int index)
        {
            return this.Collection[key][index];
        }

        public void Remove(T key)
        {
            if (this.Collection.ContainsKey(key) == false)
                return;
            foreach (J value in this.Collection[key])
                value.Dispose();
            this.Collection.Remove(key);
        }

        public void Clear()
        {
            foreach (List<J> list in this.Collection.Values)
                foreach (J value in list)
                    value.Dispose();
            this.Collection.Clear();
        }

        public IEnumerable<KeyValuePair<T, J>> EnumrateKeyAndFirstValue()
        {
            foreach (KeyValuePair<T, List<J>> kv in this.Collection)
                yield return new KeyValuePair<T, J>(kv.Key, kv.Value[0]);
        }
    }
}
