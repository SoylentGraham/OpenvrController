using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace PopX
{
	public static class Homography
	{
		public static void GetPoseFromHomography(Matrix4x4 Homography, ref Vector3 Position, ref Quaternion Rotation) 
		{ 
			Rotation = Quaternion.identity; 
			Position = Vector3.zero;

			try 
			{ 
				//  https://stackoverflow.com/questions/17027277/android-opencv-homography-to-camera-pose-considering-camera-intrinsics-and-ba 
				//Mat pose = Mat.eye(3, 4, CvType.CV_32FC1);  // 3x4 matrix, the camera pose 
				var h = Homography; 
				var norm1 = h.GetColumn(0).magnitude; 
				var norm2 = h.GetColumn(1).magnitude; 
				var tnorm = (norm1 + norm2) / 2.0f;       // Normalization value 

				System.Func<Vector4, Vector3> xyw = (v4) =>
				{
					return new Vector3(v4.x, v4.y, v4.w);
				};

				System.Func<Vector3, float, Vector3> mult = (v3, Scale) =>
				{
					var v3mult = new Vector3(v3.x,v3.y,v3.z);
					v3mult.Scale(new Vector3(Scale, Scale, Scale));
					return v3mult;
			 	};

				//  actually 3x4 so normalisation might need to be corrected (extra 0 in z!) 
				var col0 = xyw( h.GetColumn(0) ).normalized; 
				var col1 = xyw( h.GetColumn(1) ).normalized; 
				//Pose.SetColumn(0, ); 
				//Pose.SetColumn(1,); 
				var col2 = Vector3.Cross(col0, col1); 

				//double[] buffer = new double[3]; 
				//h.col(2).get(0, 0, buffer); 
				var Buffer0 = col2.x; 
				var Buffer1 = col2.y; 
				var Buffer2 = col2.z; 
				//  row 0 col 3  
				var Row0Col3 = Buffer0 / tnorm; 
				var Row1Col3 = Buffer1 / tnorm; 
				var Row2Col3 = Buffer2 / tnorm; 
				var Col3 = new Vector4(Row0Col3, Row1Col3, Row2Col3, 1); 

				Position = Col3; 
				//Rotation = Quaternion.LookRotation(col2, mult(col1,1) ); 
			} 
			catch (System.Exception e) 
			{ 
			Debug.LogException(e); 
			} 

		} 

		//	gr: turns out, this is what I was using in poptrack
		//	http://forum.openframeworks.cc/t/quad-warping-homography-without-opencv/3121/21
		//	from https://github.com/chiragraman/Unity3DProjectionMapping/blob/master/Assets/Scripts/Homography.cs
		// originally by arturo castro
		static public Matrix4x4 CalcHomography(Vector2[] src2, Vector2[] dest2)
		{
			var src = new Vector3[4];
			for (int i = 0; i < src.Length; i++)
				src[i] = src2[i];
			var dest = new Vector3[4];
			for (int i = 0; i < dest.Length; i++)
				dest[i] = dest2[i];

			// originally by arturo castro - 08/01/2010  
			//  
			// create the equation system to be solved  
			//  
			// from: Multiple View Geometry in Computer Vision 2ed  
			//       Hartley R. and Zisserman A.  
			//  
			// x' = xH  
			// where H is the homography: a 3 by 3 matrix  
			// that transformed to inhomogeneous coordinates for each point  
			// gives the following equations for each point:  
			//  
			// x' * (h31*x + h32*y + h33) = h11*x + h12*y + h13  
			// y' * (h31*x + h32*y + h33) = h21*x + h22*y + h23  
			//  
			// as the homography is scale independent we can let h33 be 1 (indeed any of the terms)  
			// so for 4 points we have 8 equations for 8 terms to solve: h11 - h32  
			// after ordering the terms it gives the following matrix  
			// that can be solved with gaussian elimination:  


			float[,] P = new float[,]{
				{-src[0].x, -src[0].y, -1,   0,   0,  0, src[0].x*dest[0].x, src[0].y*dest[0].x, -dest[0].x }, // h11  
				{  0,   0,  0, -src[0].x, -src[0].y, -1, src[0].x*dest[0].y, src[0].y*dest[0].y, -dest[0].y }, // h12  

				{-src[1].x, -src[1].y, -1,   0,   0,  0, src[1].x*dest[1].x, src[1].y*dest[1].x, -dest[1].x }, // h13  
				{  0,   0,  0, -src[1].x, -src[1].y, -1, src[1].x*dest[1].y, src[1].y*dest[1].y, -dest[1].y }, // h21  

				{-src[2].x, -src[2].y, -1,   0,   0,  0, src[2].x*dest[2].x, src[2].y*dest[2].x, -dest[2].x }, // h22  
				{  0,   0,  0, -src[2].x, -src[2].y, -1, src[2].x*dest[2].y, src[2].y*dest[2].y, -dest[2].y }, // h23  

				{-src[3].x, -src[3].y, -1,   0,   0,  0, src[3].x*dest[3].x, src[3].y*dest[3].x, -dest[3].x }, // h31  
				{  0,   0,  0, -src[3].x, -src[3].y, -1, src[3].x*dest[3].y, src[3].y*dest[3].y, -dest[3].y }, // h32  
			};

			GaussianElimination(ref P, 9);

			// gaussian elimination gives the results of the equation system  
			// in the last column of the original matrix.  
			// opengl needs the transposed 4x4 matrix:  
			float[] aux_H =
			{
				P[0,8], P[3,8], 0,  P[6,8], // h11  h21 0 h31  
				P[1,8], P[4,8], 0,  P[7,8], // h12  h22 0 h32  
				0,      0,      0,  0,     	// 0    0   0 0  
				P[2,8], P[5,8], 0,  1		// h13  h23 0 h33 
			};


			var Rows4 = new Vector4[4];

			//	gr: to let us invert, need determinet to be non zero
			var m22 = 1;
			Rows4[0].Set(P[0, 8], P[3, 8], 0, P[6, 8]);
			Rows4[1].Set(P[1, 8], P[4, 8], 0, P[7, 8]);
			Rows4[2].Set(0, 0, m22, 0);
			Rows4[3].Set(P[2, 8], P[5, 8], 0, 1);

			//	if we setrow() for each, we'll get an exception as unity checks validity of the matrix
			var HomographyMtx = new Matrix4x4(Rows4[0], Rows4[1], Rows4[2], Rows4[3]);

			return HomographyMtx;
		}



		static void GaussianElimination(ref float[,] A, int n)
		{
			// originally by arturo castro - 08/01/2010  
			//  
			// ported to c from pseudocode in  
			// http://en.wikipedia.org/wiki/Gaussian_elimination  

			int i = 0;
			int j = 0;
			int m = n - 1;
			while (i < m && j < n)
			{
				// Find pivot in column j, starting in row i:  
				int maxi = i;
				for (int k = i + 1; k < m; k++)
				{
					if (Mathf.Abs(A[k, j]) > Mathf.Abs(A[maxi, j]))
					{
						maxi = k;
					}
				}
				if (A[maxi, j] != 0)
				{
					//swap rows i and maxi, but do not change the value of i  
					if (i != maxi)
						for (int k = 0; k < n; k++)
						{
							float aux = A[i, k];
							A[i, k] = A[maxi, k];
							A[maxi, k] = aux;
						}
					//Now A[i,j] will contain the old value of A[maxi,j].  
					//divide each entry in row i by A[i,j]  
					float A_ij = A[i, j];
					for (int k = 0; k < n; k++)
					{
						A[i, k] /= A_ij;
					}
					//Now A[i,j] will have the value 1.  
					for (int u = i + 1; u < m; u++)
					{
						//subtract A[u,j] * row i from row u  
						float A_uj = A[u, j];
						for (int k = 0; k < n; k++)
						{
							A[u, k] -= A_uj * A[i, k];
						}
						//Now A[u,j] will be 0, since A[u,j] - A[i,j] * A[u,j] = A[u,j] - 1 * A[u,j] = 0.  
					}

					i++;
				}
				j++;
			}

			//back substitution  
			for (int k = m - 2; k >= 0; k--)
			{
				for (int l = k + 1; l < n - 1; l++)
				{
					A[k, m] -= A[k, l] * A[l, m];
					//A[i*n+j]=0;  
				}
			}
		}


		const int M = 9;
		const int N = 9;
		// Thos SVD code requires rows >= columns.
		//#define M 9 // rows
		//#define N 9 // cols

		static double fabs(double x)
		{
			if (x < 0)
				return -x;
			return x;
		}

		static double SIGN(double a, double b)
		{
			if (b > 0)
			{
				return fabs(a);
			}

			return -fabs(a);
		}
		static double sqrt(double x)
		{
			return Mathf.Sqrt((float)x);
		}

		static double max(double a, double b)
		{
			return (a > b) ? a : b;
		}

		static double PYTHAG(double a, double b)
		{
			double at = fabs(a), bt = fabs(b), ct, result;

			if (at > bt) { ct = bt / at; result = at * sqrt(1.0 + ct * ct); }
			else if (bt > 0.0) { ct = at / bt; result = bt * sqrt(1.0 + ct * ct); }
			else result = 0.0;
			return (result);
		}

		// Returns 1 on success, fail otherwise
		static int dsvd(float[] a, int m, int n, float[] w, float[] v)
		{
			//	float w[N];
			//	float v[N*N];

			int flag, i, its, j, jj, k, l = 0, nm = 0;
			double c, f, h, s, x, y, z;
			double anorm = 0.0, g = 0.0, scale = 0.0;
			double[] rv1 = new double[N];

			if (m < n)
			{
				//fprintf(stderr, "#rows must be > #cols \n");
				return (-1);
			}


			//	Householder reduction to bidiagonal form
			for (i = 0; i < n; i++)
			{
				//left-hand reduction
				l = i + 1;
				rv1[i] = scale * g;
				g = s = scale = 0.0;
				if (i < m)
				{
					for (k = i; k < m; k++)
						scale += Mathf.Abs(a[k * n + i]);

					if (scale != 0)
					{
						for (k = i; k < m; k++)
						{
							a[k * n + i] /= (float)scale;
							s += a[k * n + i] * a[k * n + i];
						}

						f = (double)a[i * n + i];
						g = -SIGN(sqrt(s), f);
						h = f * g - s;
						a[i * n + i] = (float)(f - g);
						if (i != n - 1)
						{
							for (j = l; j < n; j++)
							{
								for (s = 0.0, k = i; k < m; k++)
									s += ((double)a[k * n + i] * (double)a[k * n + j]);
								f = s / h;
								for (k = i; k < m; k++)
									a[k * n + j] += (float)(f * (double)a[k * n + i]);
							}
						}
						for (k = i; k < m; k++)
							a[k * n + i] = (float)((double)a[k * n + i] * scale);

					}

				}
				w[i] = (float)(scale * g);

				/// right-hand reduction
				g = s = scale = 0.0;
				if (i < m && i != n - 1)
				{
					for (k = l; k < n; k++)
						scale += fabs((double)a[i * n + k]);
					if (scale != 0)
					{
						for (k = l; k < n; k++)
						{
							a[i * n + k] = (float)((double)a[i * n + k] / scale);
							s += ((double)a[i * n + k] * (double)a[i * n + k]);
						}
						f = (double)a[i * n + l];
						g = -SIGN(sqrt(s), f);
						h = f * g - s;
						a[i * n + l] = (float)(f - g);
						for (k = l; k < n; k++)
							rv1[k] = (double)a[i * n + k] / h;
						if (i != m - 1)
						{
							for (j = l; j < m; j++)
							{
								for (s = 0.0, k = l; k < n; k++)
									s += ((double)a[j * n + k] * (double)a[i * n + k]);
								for (k = l; k < n; k++)
									a[j * n + k] += (float)(s * rv1[k]);
							}
						}
						for (k = l; k < n; k++)
							a[i * n + k] = (float)((double)a[i * n + k] * scale);
					}
				}
				anorm = max(anorm, (fabs((double)w[i]) + fabs(rv1[i])));

			}

			// accumulate the right-hand transformation
			for (i = n - 1; i >= 0; i--)
			{
				if (i < n - 1)
				{
					if (g != 0)
					{
						for (j = l; j < n; j++)
							v[j * n + i] = (float)(((double)a[i * n + j] / (double)a[i * n + l]) / g);
						// double division to avoid underflow
						for (j = l; j < n; j++)
						{
							for (s = 0.0, k = l; k < n; k++)
								s += ((double)a[i * n + k] * (double)v[k * n + j]);
							for (k = l; k < n; k++)
								v[k * n + j] += (float)(s * (double)v[k * n + i]);
						}
					}
					for (j = l; j < n; j++)
						v[i * n + j] = v[j * n + i] = 0.0f;
				}
				v[i * n + i] = 1.0f;
				g = rv1[i];
				l = i;
			}

			//accumulate the left-hand transformation
			for (i = n - 1; i >= 0; i--)
			{
				l = i + 1;
				g = (double)w[i];
				if (i < n - 1)
					for (j = l; j < n; j++)
						a[i * n + j] = 0.0f;
				if (g != 0)
				{
					g = 1.0 / g;
					if (i != n - 1)
					{
						for (j = l; j < n; j++)
						{
							for (s = 0.0, k = l; k < m; k++)
								s += ((double)a[k * n + i] * (double)a[k * n + j]);
							f = (s / (double)a[i * n + i]) * g;
							for (k = i; k < m; k++)
								a[k * n + j] += (float)(f * (double)a[k * n + i]);
						}
					}
					for (j = i; j < m; j++)
						a[j * n + i] = (float)((double)a[j * n + i] * g);
				}
				else
				{
					for (j = i; j < m; j++)
						a[j * n + i] = 0.0f;
				}
				++a[i * n + i];
			}

			// diagonalize the bidiagonal form
			for (k = n - 1; k >= 0; k--)
			{                           // loop over singular values
				for (its = 0; its < 30; its++)
				{                       // loop over allowed iterations
					flag = 1;
					for (l = k; l >= 0; l--)
					{                     // test for splitting
						nm = l - 1;
						if (fabs(rv1[l]) + anorm == anorm)
						{
							flag = 0;
							break;
						}
						if (fabs((double)w[nm]) + anorm == anorm)
							break;
					}
					if (flag != 0)
					{
						c = 0.0;
						s = 1.0;
						for (i = l; i <= k; i++)
						{
							f = s * rv1[i];
							if (fabs(f) + anorm != anorm)
							{
								g = (double)w[i];
								h = PYTHAG(f, g);
								w[i] = (float)h;
								h = 1.0 / h;
								c = g * h;
								s = (-f * h);
								for (j = 0; j < m; j++)
								{
									y = (double)a[j * n + nm];
									z = (double)a[j * n + i];
									a[j * n + nm] = (float)(y * c + z * s);
									a[j * n + i] = (float)(z * c - y * s);
								}
							}
						}
					}
					z = (double)w[k];
					if (l == k)
					{                  //convergence
						if (z < 0.0)
						{              // make singular value nonnegative
							w[k] = (float)(-z);
							for (j = 0; j < n; j++)
								v[j * n + k] = (-v[j * n + k]);
						}
						break;
					}
					if (its >= 30)
					{
						//free((void*) rv1);
						//fprintf(stderr, "No convergence after 30,000! iterations \n");
						return (0);
					}

					///shift from bottom 2 x 2 minor
					x = (double)w[l];
					nm = k - 1;
					y = (double)w[nm];
					g = rv1[nm];
					h = rv1[k];
					f = ((y - z) * (y + z) + (g - h) * (g + h)) / (2.0 * h * y);
					g = PYTHAG(f, 1.0);
					f = ((x - z) * (x + z) + h * ((y / (f + SIGN(g, f))) - h)) / x;

					// next QR transformation
					c = s = 1.0;
					for (j = l; j <= nm; j++)
					{
						i = j + 1;
						g = rv1[i];
						y = (double)w[i];
						h = s * g;
						g = c * g;
						z = PYTHAG(f, h);
						rv1[j] = z;
						c = f / z;
						s = h / z;
						f = x * c + g * s;
						g = g * c - x * s;
						h = y * s;
						y = y * c;
						for (jj = 0; jj < n; jj++)
						{
							x = (double)v[jj * n + j];
							z = (double)v[jj * n + i];
							v[jj * n + j] = (float)(x * c + z * s);
							v[jj * n + i] = (float)(z * c - x * s);
						}
						z = PYTHAG(f, h);
						w[j] = (float)z;
						if (z != 0)
						{
							z = 1.0 / z;
							c = f * z;
							s = h * z;
						}
						f = (c * g) + (s * y);
						x = (c * y) - (s * g);
						for (jj = 0; jj < m; jj++)
						{
							y = (double)a[jj * n + j];
							z = (double)a[jj * n + i];
							a[jj * n + j] = (float)(y * c + z * s);
							a[jj * n + i] = (float)(z * c - y * s);
						}
					}
					rv1[l] = 0.0;
					rv1[k] = f;
					w[k] = (float)x;
				}
			}


			return (1);
		}


		//	this was a method I was using for parallel calculations (opencl) but didnt give results as good as Arturo's openframeworks one
		static public Matrix4x4 CalcHomography_Redundant(Vector2[] src, Vector2[] dst)
		{
			// This version does not normalised the input data, which is contrary to what Multiple View Geometry says.
			// I included it to see what happens when you don't do this step.

			var X = new float[M * N]; // M,N #define inCUDA_SVD.cu

			for (int i = 0; i < 4; i++)
			{
				float srcx = src[i].x;
				float srcy = src[i].y;
				float dstx = dst[i].x;
				float dsty = dst[i].y;

				int y1 = (i * 2 + 0) * N;
				int y2 = (i * 2 + 1) * N;

				// First row
				X[y1 + 0] = 0.0f;
				X[y1 + 1] = 0.0f;
				X[y1 + 2] = 0.0f;

				X[y1 + 3] = -srcx;
				X[y1 + 4] = -srcy;
				X[y1 + 5] = -1.0f;

				X[y1 + 6] = dsty * srcx;
				X[y1 + 7] = dsty * srcy;
				X[y1 + 8] = dsty;

				// Second row
				X[y2 + 0] = srcx;
				X[y2 + 1] = srcy;
				X[y2 + 2] = 1.0f;

				X[y2 + 3] = 0.0f;
				X[y2 + 4] = 0.0f;
				X[y2 + 5] = 0.0f;

				X[y2 + 6] = -dstx * srcx;
				X[y2 + 7] = -dstx * srcy;
				X[y2 + 8] = -dstx;
			}

			// Fill the last row
			float srcx2 = src[3].x;
			float srcy2 = src[3].y;
			float dstx2 = dst[3].x;
			float dsty2 = dst[3].y;

			int y = 8 * N;
			X[y + 0] = -dsty2 * srcx2;
			X[y + 1] = -dsty2 * srcy2;
			X[y + 2] = -dsty2;

			X[y + 3] = dstx2 * srcx2;
			X[y + 4] = dstx2 * srcy2;
			X[y + 5] = dstx2;

			X[y + 6] = 0;
			X[y + 7] = 0;
			X[y + 8] = 0;

			float[] w = new float[N];
			float[] v = new float[N * N];

			//	float16
			Matrix4x4 ret_H = new Matrix4x4(Vector4.zero, Vector4.zero, Vector4.zero, Vector4.zero);
			int ret = dsvd(X, M, N, w, v);

			if (ret == 1)
			{
				// Sort
				float smallest = w[0];
				int col = 0;

				for (int i = 1; i < N; i++)
				{
					if (w[i] < smallest)
					{
						smallest = w[i];
						col = i;
					}
				}

				ret_H[0] = v[0 * N + col];
				ret_H[1] = v[1 * N + col];
				ret_H[2] = v[2 * N + col];
				ret_H[3] = v[3 * N + col];
				ret_H[4] = v[4 * N + col];
				ret_H[5] = v[5 * N + col];
				ret_H[6] = v[6 * N + col];
				ret_H[7] = v[7 * N + col];
				ret_H[8] = v[8 * N + col];
			}

			return ret_H;
		}
	}
}



