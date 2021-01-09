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
using System.Text.RegularExpressions;

namespace FooEditEngine
{
    /// <summary>
    /// IMarkerPattern�C���^�[�t�F�C�X
    /// </summary>
    public interface IMarkerPattern
    {
        /// <summary>
        /// �}�[�J�[��Ԃ�
        /// </summary>
        /// <param name="lineHeadIndex">�s���ւ̃C���f�b�N�X��\��</param>
        /// <param name="s">������</param>
        /// <returns>Marker�񋓑̂�Ԃ�</returns>
        IEnumerable<Marker> GetMarker(int lineHeadIndex, string s);
    }
    /// <summary>
    /// ���K�\���Ń}�[�J�[�̎擾���s���N���X
    /// </summary>
    public sealed class RegexMarkerPattern : IMarkerPattern
    {
        Regex regex;
        HilightType type;
        Color color;
        /// <summary>
        /// �R���X�g���N�^�[
        /// </summary>
        /// <param name="regex">regex�I�u�W�F�N�g</param>
        /// <param name="type">�n�C���C�g�^�C�v</param>
        /// <param name="color">�F</param>
        public RegexMarkerPattern(Regex regex,HilightType type,Color color)
        {
            this.regex = regex;
            this.type = type;
            this.color = color;
        }

        /// <summary>
        /// �}�[�J�[��Ԃ�
        /// </summary>
        /// <param name="lineHeadIndex">�s���ւ̃C���f�b�N�X��\��</param>
        /// <param name="s">������</param>
        /// <returns>Marker�񋓑̂�Ԃ�</returns>
        public IEnumerable<Marker> GetMarker(int lineHeadIndex, string s)
        {
            foreach (Match m in this.regex.Matches(s))
            {
                yield return Marker.Create(lineHeadIndex + m.Index, m.Length, this.type,this.color);
            }
        }
    }
    /// <summary>
    /// MarkerPattern�Z�b�g
    /// </summary>
    public sealed class MarkerPatternSet
    {
        MarkerCollection markers;
        Dictionary<int, IMarkerPattern> watchDogSet = new Dictionary<int, IMarkerPattern>();

        internal MarkerPatternSet(LineToIndexTable lti,MarkerCollection markers)
        {
            this.markers = markers;
        }

        internal IEnumerable<Marker> GetMarkers(CreateLayoutEventArgs e)
        {
            foreach (int id in this.watchDogSet.Keys)
            {
                foreach (Marker m in this.watchDogSet[id].GetMarker(e.Index, e.Content))
                    yield return m;
            }
        }

        internal event EventHandler Updated;

        /// <summary>
        /// WatchDog��ǉ�����
        /// </summary>
        /// <param name="id">�}�[�J�[ID</param>
        /// <param name="dog">IMarkerPattern�C���^�[�t�F�C�X</param>
        public void Add(int id, IMarkerPattern dog)
        {
            this.watchDogSet.Add(id, dog);
            this.Updated(this, null);
        }

        /// <summary>
        /// �}�[�J�[ID���܂܂�Ă��邩�ǂ����𒲂ׂ�
        /// </summary>
        /// <param name="id">�}�[�J�[ID</param>
        /// <returns>�܂܂�Ă���ΐ^�B�����łȂ���΋U</returns>
        public bool Contains(int id)
        {
            return this.watchDogSet.ContainsKey(id);
        }

        /// <summary>
        /// WatchDog��ǉ�����
        /// </summary>
        /// <param name="id">�}�[�J�[ID</param>
        public void Remove(int id)
        {
            this.markers.Clear(id);
            this.watchDogSet.Remove(id);
            this.Updated(this, null);
        }
    }
}