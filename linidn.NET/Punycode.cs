/// <summary> Copyright (C) 2004  Free Software Foundation, Inc.
/// *
/// Author: Alexander Gnauck AG-Software
/// *
/// This file is part of GNU Libidn.
/// *
/// This library is free software; you can redistribute it and/or
/// modify it under the terms of the GNU Lesser General Public License
/// as published by the Free Software Foundation; either version 2.1 of
/// the License, or (at your option) any later version.
/// *
/// This library is distributed in the hope that it will be useful, but
/// WITHOUT ANY WARRANTY; without even the implied warranty of
/// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
/// Lesser General Public License for more details.
/// *
/// You should have received a copy of the GNU Lesser General Public
/// License along with this library; if not, write to the Free Software
/// Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301
/// USA
/// </summary>

using System;

namespace gnu.inet.encoding
{	
	
	public class Punycode
	{
		/* Punycode parameters */
		internal const int TMIN = 1;
		internal const int TMAX = 26;
		internal const int BASE = 36;
		internal const int INITIAL_N = 128;
		internal const int INITIAL_BIAS = 72;
		internal const int DAMP = 700;
		internal const int SKEW = 38;
		internal const char DELIMITER = '-';
		
		/// <summary> Punycodes a unicode string.
		/// *
		/// </summary>
		/// <param name="input">Unicode string.
		/// </param>
		/// <returns> Punycoded string.
		/// 
		/// </returns>
		public static System.String encode(System.String input)
		{
			int n = INITIAL_N;
			int delta = 0;
			int bias = INITIAL_BIAS;
			System.Text.StringBuilder output = new System.Text.StringBuilder();
			
			// Copy all basic code points to the output
			int b = 0;
			for (int i = 0; i < input.Length; i++)
			{
				char c = input[i];
				if (isBasic(c))
				{
					output.Append(c);
					b++;
				}
			}
			
			// Append delimiter
			if (b > 0)
			{
				output.Append(DELIMITER);
			}
			
			int h = b;
			while (h < input.Length)
			{
				int m = System.Int32.MaxValue;
				
				// Find the minimum code point >= n
				for (int i = 0; i < input.Length; i++)
				{
					int c = input[i];
					if (c >= n && c < m)
					{
						m = c;
					}
				}
				
				if (m - n > (System.Int32.MaxValue - delta) / (h + 1))
				{
					throw new PunycodeException(PunycodeException.OVERFLOW);
				}
				delta = delta + (m - n) * (h + 1);
				n = m;
				
				for (int j = 0; j < input.Length; j++)
				{
					int c = input[j];
					if (c < n)
					{
						delta++;
						if (0 == delta)
						{
							throw new PunycodeException(PunycodeException.OVERFLOW);
						}
					}
					if (c == n)
					{
						int q = delta;
						
						for (int k = BASE; ; k += BASE)
						{
							int t;
							if (k <= bias)
							{
								t = TMIN;
							}
							else if (k >= bias + TMAX)
							{
								t = TMAX;
							}
							else
							{
								t = k - bias;
							}
							if (q < t)
							{
								break;
							}
							output.Append((char) digit2codepoint(t + (q - t) % (BASE - t)));
							q = (q - t) / (BASE - t);
						}
						
						output.Append((char) digit2codepoint(q));
						bias = adapt(delta, h + 1, h == b);
						delta = 0;
						h++;
					}
				}
				
				delta++;
				n++;
			}
			
			return output.ToString();
		}
		
		/// <summary> Decode a punycoded string.
		/// *
		/// </summary>
		/// <param name="input">Punycode string
		/// </param>
		/// <returns> Unicode string.
		/// 
		/// </returns>
		public static System.String decode(System.String input)
		{
			int n = INITIAL_N;
			int i = 0;
			int bias = INITIAL_BIAS;
			System.Text.StringBuilder output = new System.Text.StringBuilder();
			
			int d = input.LastIndexOf((System.Char) DELIMITER);
			if (d > 0)
			{
				for (int j = 0; j < d; j++)
				{
					char c = input[j];
					if (!isBasic(c))
					{
						throw new PunycodeException(PunycodeException.BAD_INPUT);
					}
					output.Append(c);
				}
				d++;
			}
			else
			{
				d = 0;
			}
			
			while (d < input.Length)
			{
				int oldi = i;
				int w = 1;
				
				for (int k = BASE; ; k += BASE)
				{
					if (d == input.Length)
					{
						throw new PunycodeException(PunycodeException.BAD_INPUT);
					}
					int c = input[d++];
					int digit = codepoint2digit(c);
					if (digit > (System.Int32.MaxValue - i) / w)
					{
						throw new PunycodeException(PunycodeException.OVERFLOW);
					}
					
					i = i + digit * w;
					
					int t;
					if (k <= bias)
					{
						t = TMIN;
					}
					else if (k >= bias + TMAX)
					{
						t = TMAX;
					}
					else
					{
						t = k - bias;
					}
					if (digit < t)
					{
						break;
					}
					w = w * (BASE - t);
				}
				
				bias = adapt(i - oldi, output.Length + 1, oldi == 0);
				
				if (i / (output.Length + 1) > System.Int32.MaxValue - n)
				{
					throw new PunycodeException(PunycodeException.OVERFLOW);
				}
				
				n = n + i / (output.Length + 1);
				i = i % (output.Length + 1);
				output.Insert(i, (char) n);
				i++;
			}
			
			return output.ToString();
		}
		
		public static int adapt(int delta, int numpoints, bool first)
		{
			if (first)
			{
				delta = delta / DAMP;
			}
			else
			{
				delta = delta / 2;
			}
			
			delta = delta + (delta / numpoints);
			
			int k = 0;
			while (delta > ((BASE - TMIN) * TMAX) / 2)
			{
				delta = delta / (BASE - TMIN);
				k = k + BASE;
			}
			
			return k + ((BASE - TMIN + 1) * delta) / (delta + SKEW);
		}
		
		public static bool isBasic(char c)
		{
			return c < 0x80;
		}
		
		public static int digit2codepoint(int d)
		{
			if (d < 26)
			{
				// 0..25 : 'a'..'z'
				return d + 'a';
			}
			else if (d < 36)
			{
				// 26..35 : '0'..'9';
				return d - 26 + '0';
			}
			else
			{
				throw new PunycodeException(PunycodeException.BAD_INPUT);
			}
		}
		
		public static int codepoint2digit(int c)
		{
			if (c - '0' < 10)
			{
				// '0'..'9' : 26..35
				return c - '0' + 26;
			}
			else if (c - 'a' < 26)
			{
				// 'a'..'z' : 0..25
				return c - 'a';
			}
			else
			{
				throw new PunycodeException(PunycodeException.BAD_INPUT);
			}
		}
	}
}