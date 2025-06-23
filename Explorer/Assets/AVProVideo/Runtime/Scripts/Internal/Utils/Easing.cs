//-----------------------------------------------------------------------------
// Copyright 2015-2024 RenderHeads Ltd.  All rights reserved.
//-----------------------------------------------------------------------------

using UnityEngine;

namespace RenderHeads.Media.AVProVideo
{
	/// <summary>
	/// Easing functions
	/// </summary>
	// [System.Serializable]
	public static class Easing
	{
		// public Preset preset = Preset.Linear;

		public enum Preset
		{
			Step,
			Linear,
			InQuad,
			OutQuad,
			InOutQuad,
			InCubic,
			OutCubic,
			InOutCubic,
			InQuint,
			OutQuint,
			InOutQuint,
			InQuart,
			OutQuart,
			InOutQuart,
			InExpo,
			OutExpo,
			InOutExpo,
			Random,
			RandomNotStep,
		}

		public static System.Func<float, float> GetFunction(Preset preset)
		{
			System.Func<float, float> result = null;
			switch (preset)
			{
				case Preset.Step:
					result = Step;
					break;
				
				case Preset.Linear:
					result = Linear;
					break;
				
				case Preset.InQuad:
					result = InQuad;
					break;
				
				case Preset.OutQuad:
					result = OutQuad;
					break;
				
				case Preset.InOutQuad:
					result = InOutQuad;
					break;
				
				case Preset.InCubic:
					result = InCubic;
					break;
				
				case Preset.OutCubic:
					result = OutCubic;
					break;
				
				case Preset.InOutCubic:
					result = InOutCubic;
					break;
				
				case Preset.InQuint:
					result = InQuint;
					break;
				
				case Preset.OutQuint:
					result = OutQuint;
					break;
				
				case Preset.InOutQuint:
					result = InOutQuint;
					break;
				
				case Preset.InQuart:
					result = InQuart;
					break;
				
				case Preset.OutQuart:
					result = OutQuart;
					break;
				
				case Preset.InOutQuart:
					result = InOutQuart;
					break;
				
				case Preset.InExpo:
					result = InExpo;
					break;
				
				case Preset.OutExpo:
					result = OutExpo;
					break;
				
				case Preset.InOutExpo:
					result = InOutExpo;
					break;
				
				case Preset.Random:
					result = GetFunction((Preset)Random.Range(0, (int)Preset.Random));
					break;
				
				case Preset.RandomNotStep:
					result = GetFunction((Preset)Random.Range((int)Preset.Step+1, (int)Preset.Random));
					break;
			}
			
			return result;
		}

		public static float PowerEaseIn(float t, float power)
		{
			return Mathf.Pow(t, power);
		}

		public static float PowerEaseOut(float t, float power)
		{
			return 1f - Mathf.Abs(Mathf.Pow(t - 1f, power));
		}

		public static float PowerEaseInOut(float t, float power)
		{
			float result;
			if (t < 0.5f)
			{
				result = PowerEaseIn(t * 2f, power) / 2f;
			}
			else
			{
				result = PowerEaseOut(t * 2f - 1f, power) / 2f + 0.5f;
			}
			return result;
		}

		public static float Step(float t)
		{
			float result = 0f;
			if (t >= 0.5f)
			{
				result = 1f;
			}
			return result;
		}

		public static float Linear(float t)
		{
			return t;
		}

		public static float InQuad(float t)
		{
			return PowerEaseIn(t, 2f);
		}

		public static float OutQuad(float t)
		{
			return PowerEaseOut(t, 2f);
		}

		public static float InOutQuad(float t)
		{
			return PowerEaseInOut(t, 2f);
		}

		public static float InCubic(float t)
		{
			return PowerEaseIn(t, 3f);
		}

		public static float OutCubic(float t)
		{
			return PowerEaseOut(t, 3f);
		}

		public static float InOutCubic(float t)
		{
			return PowerEaseInOut(t, 3f);
		}

		public static float InQuart(float t)
		{
			return PowerEaseIn(t, 4f);
		}

		public static float OutQuart(float t)
		{
			return PowerEaseOut(t, 4f);
		}

		public static float InOutQuart(float t)
		{
			return PowerEaseInOut(t, 4f);
		}

		public static float InQuint(float t)
		{
			return PowerEaseIn(t, 5f);
		}

		public static float OutQuint(float t)
		{
			return PowerEaseOut(t, 5f);
		}

		public static float InOutQuint(float t)
		{
			return PowerEaseInOut(t, 5f);
		}

		public static float InExpo(float t)
		{
			float result = 0f;
			if (t != 0f)
			{
				result = Mathf.Pow(2f, 10f * (t - 1f));
			}
			return result;
		}

		public static float OutExpo(float t)
		{
			float result = 1f;
			if (t != 1f)
			{
				result = -Mathf.Pow(2f, -10f * t) + 1f;
			}
			return result;
		}

		public static float InOutExpo(float t)
		{
			float result = 0f;
			if (t > 0f)
			{
				result = 1f;
				if (t < 1f)
				{
					t *= 2f;
					if (t < 1f)
					{
						result = 0.5f * Mathf.Pow(2f, 10f * (t - 1f));
					}
					else
					{
						t--;
						result = 0.5f * (-Mathf.Pow(2f, -10f * t) + 2f);
					}
				}
			}
			return result;
		}
	}

}
