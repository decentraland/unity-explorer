//-----------------------------------------------------------------------------
// Copyright 2015-2024 RenderHeads Ltd.  All rights reserved.
//-----------------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace RenderHeads.Media.AVProVideo
{
	public enum VideoRange
	{
		SDR,
		HLG,
		PQ
	}

	public enum CodecType: uint
	{
		ac_3 = 0x61632d33,
		alac = 0x616c6163,
		avc1 = 0x61766331,
		avc3 = 0x61766333,
		dvh1 = 0x64766831,
		dvhe = 0x64766865,
		ec_3 = 0x65632d33,
		fLaC = 0x664c6143,
		hev1 = 0x68657631,
		hvc1 = 0x68766331,
		mjpg = 0x6d6a7067,
		mp4a = 0x6d703461,
		stpp = 0x73747070,
		wvtt = 0x77767474,
		unknown = 0
	}

	public class Variant
	{
		private int m_iId = -1;
		private int m_iWidth = 0;
		private int m_iHeight = 0;
		private int m_iPeakDataRate = 0;
		private int m_iAverageDataRate = 0;
		private CodecType m_VideoCodecType = CodecType.unknown;
		private float m_fFrameRate = 0;
		private VideoRange m_eVideoRange;
		private CodecType m_AudioCodecType = CodecType.unknown;

		public Variant(
			int iId,
			int iWidth,
			int iHeight,
			int iPeakDataRate,
			int iAverageDataRate = 0,
			CodecType videoCodecType = CodecType.unknown,
			float fFrameRate = 0,
			VideoRange eVideoRange = VideoRange.SDR,
			CodecType audioCodecType = CodecType.unknown
		)
		{
			m_iId = iId;
			m_iWidth = iWidth;
			m_iHeight = iHeight;
			m_iPeakDataRate = iPeakDataRate;
			m_iAverageDataRate = iAverageDataRate;
			m_VideoCodecType = videoCodecType;
			m_fFrameRate = fFrameRate;
			m_eVideoRange = eVideoRange;
			m_AudioCodecType = audioCodecType;
		}

		public int Id
		{
			get { return m_iId; }
		}

		public int Width
		{
			get { return m_iWidth; }
		}

		public int Height
		{
			get { return m_iHeight; }
		}

		public int PeakDataRate
		{
			get { return m_iPeakDataRate; }
		}

		public int AverageDataRate
		{
			get { return m_iAverageDataRate; }
		}

		public float FrameRate
		{
			get { return m_fFrameRate; }
		}

		public VideoRange VideoRange
		{
			get { return m_eVideoRange; }
		}

		public CodecType VideoCodecType
		{
			get { return m_VideoCodecType; }
		}

		public string VideoCodecName
		{
			get
			{
				switch (m_VideoCodecType)
				{
					case CodecType.avc1:
					case CodecType.avc3:
						return "H264";

					case CodecType.dvh1:
					case CodecType.dvhe:
						return "Dolby Vision";

					case CodecType.hev1:
					case CodecType.hvc1:
						return "HEVC";
					
					case CodecType.mjpg:
						return "MJPEG";

					default:
						return "";
				}
			}
		}

		public CodecType AudioCodecType
		{
			get { return m_AudioCodecType; }
		}

		public string AudioCodecName
		{
			get
			{
				switch (m_AudioCodecType)
				{
					case CodecType.ac_3:
						return "AC-3";

					case CodecType.alac:
						return "Apple Lossless";

					case CodecType.ec_3:
						return "EC-3";
					
					case CodecType.fLaC:
						return "FLAC";

					case CodecType.mp4a:
						// Could be something else but this is most likely, requires passing audio subtype
						return "AAC";

					default:
						return "";
				}
			}
		}

		private static Variant s_Auto = new Variant(-1, 0, 0, 0);
		public static Variant Auto
		{
			get { return s_Auto; }
		}
	}

	public interface IVariants: IEnumerable
	{
		int Count { get; }
		Variant Current { get; }
		Variant this[int index] { get; }
		Variant GetSelectedVariant();
		void SelectVariant(Variant variant);
	}

	public partial class BaseMediaPlayer : IVariants
	{
		protected List<Variant> _variants = new List<Variant>();

		public int Count
		{
			get
			{
				return _variants.Count;
			}
		}

		public Variant Current
		{
			get
			{
				return GetSelectedVariant();
			}
		}

		public Variant this[int index]
		{
			get
			{
				return _variants[index];
			}
		}

		public virtual IEnumerator GetEnumerator()
		{
			return _variants.GetEnumerator();
		}

		public virtual Variant GetSelectedVariant()
		{
			return Variant.Auto;
		}

		public virtual void SelectVariant(Variant variant)
		{

		}

		protected virtual void UpdateVariants()
		{
			_variants.Clear();
			int count = InternalGetVariantCount();
			for (int i = 0; i < count; ++i)
			{
				Variant variant = InternalGetVariantAtIndex(i);
				if (variant != null)
				{
					_variants.Add(variant);
				}
			}

			// Sort the list by codec, then peak bitrate
			_variants.Sort( (x, y) => 
			{
				int iReturn = x.VideoCodecType.CompareTo( y.VideoCodecType );
				if( iReturn == 0 ) 
				{
					iReturn = x.PeakDataRate.CompareTo( y.PeakDataRate );
				}
				return iReturn;
			} );
		}

		internal virtual int InternalGetVariantCount()
		{
			return 0;
		}

		internal virtual Variant InternalGetVariantAtIndex(int index)
		{
			return null;
		}
	}
}
