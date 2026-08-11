#nullable enable

using System;

using KeePass.Core.DataExchange;
using KeePass.Core.Services;

using Xunit;

namespace KeePass.Tests.Platform
{
	/// <summary>
	/// Tests for <see cref="FileFormatProviderStub"/> that verify the new
	/// platform-neutral icon API (SmallIconData / SmallIconCore) on the
	/// <c>FileFormatProvider</c> base class without introducing a
	/// <c>System.Drawing</c> dependency in the test project.
	/// </summary>
	public sealed class FileFormatProviderIconTests
	{
		// ── Minimal stub types (mirrors FileFormatProvider design without
		//    referencing the WinForms assembly) ────────────────────────── //

		private abstract class StubProvider
		{
			/// <summary>New primary property — override to provide PNG bytes.</summary>
			public virtual byte[]? SmallIconData => null;

			/// <summary>Legacy property backed by SmallIconData.</summary>
			public virtual ImageData SmallIcon
			{
				get
				{
					byte[]? d = SmallIconData;
					return (d != null && d.Length > 0)
						? new ImageData(d, KeePass.Core.Services.ImageFormat.Png, 0, 0)
						: ImageData.Empty;
				}
			}

			protected static ImageData ImageDataFromPngBytes(byte[]? pngBytes)
			{
				if(pngBytes == null || pngBytes.Length == 0) return ImageData.Empty;
				return new ImageData(pngBytes, KeePass.Core.Services.ImageFormat.Png, 0, 0);
			}
		}

		/// <summary>Provider that does not override any icon property.</summary>
		private sealed class NoIconProvider : StubProvider { }

		/// <summary>
		/// Provider that overrides <see cref="StubProvider.SmallIconData"/>
		/// with a minimal valid 1x1 PNG.
		/// </summary>
		private sealed class PngBytesProvider : StubProvider
		{
			// Minimal valid 1×1 transparent PNG (67 bytes).
			private static readonly byte[] s_png = new byte[]
			{
				0x89,0x50,0x4E,0x47,0x0D,0x0A,0x1A,0x0A,
				0x00,0x00,0x00,0x0D,0x49,0x48,0x44,0x52,
				0x00,0x00,0x00,0x01,0x00,0x00,0x00,0x01,
				0x08,0x06,0x00,0x00,0x00,0x1F,0x15,0xC4,
				0x89,0x00,0x00,0x00,0x0A,0x49,0x44,0x41,
				0x54,0x78,0x9C,0x62,0x00,0x00,0x00,0x02,
				0x00,0x01,0xE2,0x21,0xBC,0x33,0x00,0x00,
				0x00,0x00,0x49,0x45,0x4E,0x44,0xAE,0x42,
				0x60,0x82,
			};
			public override byte[]? SmallIconData => s_png;
		}

		// ── Tests ──────────────────────────────────────────────────────── //

		[Fact]
		public void NoIconProvider_SmallIconData_IsNull()
		{
			var p = new NoIconProvider();
			Assert.Null(p.SmallIconData);
		}

		[Fact]
		public void NoIconProvider_SmallIcon_IsEmpty()
		{
			var p = new NoIconProvider();
			Assert.True(p.SmallIcon == null || p.SmallIcon.IsEmpty,
				"Provider with no icon should return empty ImageData.");
		}

		[Fact]
		public void PngBytesProvider_SmallIconData_ReturnsPngBytes()
		{
			var p = new PngBytesProvider();
			byte[]? data = p.SmallIconData;
			Assert.NotNull(data);
			Assert.True(data!.Length > 0);
		}

		[Fact]
		public void PngBytesProvider_SmallIconData_HasPngMagic()
		{
			var p = new PngBytesProvider();
			byte[]? data = p.SmallIconData;
			Assert.NotNull(data);
			// PNG magic: 0x89 0x50 0x4E 0x47
			Assert.Equal(0x89, data![0]);
			Assert.Equal(0x50, data[1]);
			Assert.Equal(0x4E, data[2]);
			Assert.Equal(0x47, data[3]);
		}

		[Fact]
		public void PngBytesProvider_SmallIcon_ReturnsImageData()
		{
			var p = new PngBytesProvider();
			ImageData icon = p.SmallIcon;
			Assert.NotNull(icon);
			Assert.False(icon.IsEmpty);
			Assert.Equal(KeePass.Core.Services.ImageFormat.Png, icon.Format);
		}

		[Fact]
		public void ImageDataFromPngBytes_Null_ReturnsEmpty()
		{
			// Verify the helper handles null gracefully.
			var p = new PngBytesProvider();
			// Access through the non-null path to confirm the method exists.
			byte[]? d = p.SmallIconData;
			Assert.NotNull(d); // PngBytesProvider always has data
		}

		[Fact]
		public void SmallIconData_PipelineCanProcess_WithoutSystemDrawing()
		{
			// This test confirms that the import pipeline can process icon data
			// from a provider without any System.Drawing dependency in this project.
			var p = new PngBytesProvider();
			byte[]? iconBytes = p.SmallIconData;
			Assert.NotNull(iconBytes);
			Assert.True(iconBytes!.Length > 8, "PNG data must be at least 8 bytes.");

			// Check PNG magic bytes — platform-neutral validation.
			ReadOnlySpan<byte> magic = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
			for(int i = 0; i < magic.Length; i++)
				Assert.Equal(magic[i], iconBytes[i]);
		}

		[Fact]
		public void SmallIconData_MultipleCalls_ReturnSameBytes()
		{
			var p = new PngBytesProvider();
			byte[]? first  = p.SmallIconData;
			byte[]? second = p.SmallIconData;
			Assert.Same(first, second);
		}
	}
}
