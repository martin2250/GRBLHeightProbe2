﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GrblHeightProbe2
{
	class HeightMap
	{
		const string FileHeader = "HeightMapV2.0 Do not modify!";

		private float[,] Value;
		private bool[,] HasValue;

		public int SizeX { get; private set; }
		public int SizeY { get; private set; }

		public float GridSize { get; private set; }

		public float OffsetX { get; private set; }
		public float OffsetY { get; private set; }

		public float MaxZ { get; private set; }
		public float MinZ { get; private set; }

		public float MinX { get { return OffsetX; } }
		public float MinY { get { return OffsetY; } }
		public float MaxX { get { return (SizeX - 1) * GridSize + OffsetX; } }
		public float MaxY { get { return (SizeY - 1) * GridSize + OffsetY; } }

		public event Action OnPointAdded;

		public Queue<Point> NotProbed { get; private set; }

		public HeightMap(int sizeX, int sizeY, float gridSize, float offsetX, float offsetY)
		{
			Value = new float[sizeX, sizeY];
			HasValue = new bool[sizeX, sizeY];

			SizeX = sizeX;
			SizeY = sizeY;

			GridSize = gridSize;

			OffsetX = offsetX;
			OffsetY = offsetY;

			MaxZ = float.MinValue;
			MinZ = float.MaxValue;

			NotProbed = new Queue<Point>(SizeX * SizeY);

			int x = 0;

			while (x < SizeX)
			{
				for (int y = 0; y < SizeY; y++)
					NotProbed.Enqueue(new Point(x, y));

				if (++x >= SizeX)
					break;

				for (int y = SizeY - 1; y >= 0; y--)
					NotProbed.Enqueue(new Point(x, y));

				x++;
			}


		}

		public HeightMap(BinaryReader file)
		{
			if (file.ReadString() != FileHeader)
			{
				throw new FileLoadException("The file header does not match!");
			}

			SizeX = file.ReadInt32();
			SizeY = file.ReadInt32();

			Value = new float[SizeX, SizeY];
			HasValue = new bool[SizeX, SizeY];

			GridSize = file.ReadSingle();

			OffsetX = file.ReadSingle();
			OffsetY = file.ReadSingle();

			MaxZ = float.MinValue;
			MinZ = float.MaxValue;

			for (int x = 0; x < SizeX; x++)
			{
				for (int y = 0; y < SizeY; y++)
				{
					float point = file.ReadSingle();

					if (HasValue[x, y] = file.ReadBoolean())
					{
						this[x, y] = point;
					}
				}
			}

			file.Close();

			NotProbed = new Queue<Point>(SizeX * SizeY);
			for (int x = 0; x < SizeX; )
			{
				for (int y = 0; y < SizeY; y++)
					if (!HasValue[x, y])
						NotProbed.Enqueue(new Point(x, y));

				if (++x >= SizeX)
					break;

				for (int y = SizeY - 1; y >= 0; y--)
					if (!HasValue[x, y])
						NotProbed.Enqueue(new Point(x, y));

				x++;
			}
		}

		public void Save(BinaryWriter file)
		{
			file.Write(FileHeader);

			file.Write(SizeX);
			file.Write(SizeY);

			file.Write(GridSize);

			file.Write(OffsetX);
			file.Write(OffsetY);

			for (int x = 0; x < SizeX; x++)
			{
				for (int y = 0; y < SizeY; y++)
				{
					file.Write(this[x, y]);
					file.Write(HasValue[x, y]);
				}
			}

			file.Close();
		}

		public float this[int x, int y]
		{
			get
			{
				return Value[x, y];
			}
			set
			{
				Value[x, y] = value;
				HasValue[x, y] = true;

				if (value > MaxZ)
					MaxZ = value;

				if (value < MinZ)
					MinZ = value;

				if (OnPointAdded != null)
					foreach (Action a in OnPointAdded.GetInvocationList())
						a();
			}
		}

		public PointF GetCoordinates(Point point)
		{
			return new PointF(point.X * GridSize + OffsetX, point.Y * GridSize + OffsetY);
		}

		public PointF GetCoordinates(float x, float y)
		{
			return new PointF(x * GridSize + OffsetX, y * GridSize + OffsetY);
		}

		private Color ColorFromHeight(float val)
		{
			if (MaxZ > MinZ)
			{
				val -= MinZ;
				val /= (MaxZ - MinZ);
			}
			else
				return Color.Green;

			float r = 0, g = 0, b = 0;

			if (val < 0.2)
			{
				b = 1;
				g = 5 * val;
			}
			else if (val < 0.4)
			{
				val -= 0.2F;

				g = 1;
				b = 1 - 5 * val;
			}
			else if (val < 0.6)
			{
				val -= 0.4F;

				g = 1;
				r = 5 * val;
			}
			else if (val < 0.8)
			{
				val -= 0.6F;

				r = 1;
				g = 1 - 5 * val;
			}
			else
			{
				val -= 0.8F;

				r = 1;
				b = 5 * val;
			}

			return Color.FromArgb(255, (int)(r * 255), (int)(g * 255), (int)(b * 255));
		}

		public Bitmap GetPreview()
		{
			const int IMG_CellSize = 80;
			const int IMG_CellBorder = 3;

			Bitmap i = new Bitmap(SizeX * IMG_CellSize, SizeY * IMG_CellSize);
			Graphics gfx = Graphics.FromImage(i);

			Brush Background = new SolidBrush(Color.Gray);

			Brush Text = new SolidBrush(Color.Black);

			StringFormat TextSF = new StringFormat();
			TextSF.Alignment = StringAlignment.Center;

			Font font = new Font("Comic Sans", 11);

			gfx.FillRectangle(Background, 0, 0, i.Size.Width, i.Size.Height);

			for (int x = 0; x < SizeX; x++)
			{
				for (int y = 0; y < SizeY; y++)
				{
					if (HasValue[x, y])
					{
						using (Brush Cell = new SolidBrush(ColorFromHeight(this[x, y])))
						{
							gfx.FillRectangle(Cell, x * IMG_CellSize + IMG_CellBorder, (SizeY - 1 - y) * IMG_CellSize + IMG_CellBorder, IMG_CellSize - 2 * IMG_CellBorder, IMG_CellSize - 2 * IMG_CellBorder);
						}

						gfx.DrawString(
							this[x, y].ToString("F2"),
							font,
							Text,
							x * IMG_CellSize + IMG_CellSize / 2,
							(SizeY - 1 - y) * IMG_CellSize + IMG_CellSize / 2,
							TextSF
						);
					}

					PointF Coordinates = GetCoordinates(x, y);

					gfx.DrawString(
						string.Format("({0:F2}|{1:F2})", Coordinates.X, Coordinates.Y),
						font,
						Text,
						x * IMG_CellSize + IMG_CellSize / 2,
						(SizeY - 1 - y) * IMG_CellSize + IMG_CellSize / 2 - font.Height,
						TextSF
					);
				}
			}

			gfx.Dispose();
			Background.Dispose();
			Text.Dispose();
			font.Dispose();
			TextSF.Dispose();
			return i;
		}

		public float GetHeightAt(float x, float y)
		{
			x = (x - OffsetX) / GridSize;
			y = (y - OffsetY) / GridSize;

			int iLX = (int)Math.Floor(x);	//lower integer part
			int iLY = (int)Math.Floor(y);

			int iHX = (int)Math.Ceiling(x);	//upper integer part
			int iHY = (int)Math.Ceiling(y);

			float fX = x - iLX;				//fractional part
			float fY = y - iLY;

			float linUpper = this[iHX, iHY] * fX + this[iHX, iLY] * (1 - fX);		//linear immediates
			float linLower = this[iLX, iHY] * fX + this[iLX, iLY] * (1 - fX);

			return linUpper * fY + linLower * (1 - fY);		//bilinear result
		}

		public float GetHeightAt(PointF position)
		{
			return GetHeightAt(position.X, position.Y);
		}

	}
}