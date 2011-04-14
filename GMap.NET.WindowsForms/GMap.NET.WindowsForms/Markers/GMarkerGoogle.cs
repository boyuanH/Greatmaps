
namespace GMap.NET.WindowsForms.Markers
{
   using System.Drawing;

#if !PocketPC
   using System.Windows.Forms.Properties;
#else
   using GMap.NET.WindowsMobile.Properties;
#endif

   public enum GoogleMarkerType
   {
      arrow,
      blue,
      blue_small,
      blue_dot,
      blue_pushpin,
      brown_small,
      gray_small,
      green,
      green_small,
      green_dot,
      green_pushpin,
      green_big_go,
      yellow,
      yellow_small,
      yellow_dot,
      yellow_big_pause,
      yellow_pushpin,
      lightblue,
      lightblue_dot,
      lightblue_pushpin,
      orange,
      orange_small,
      orange_dot,
      pink,
      pink_dot,
      pink_pushpin,
      purple,
      purple_small,
      purple_dot,
      purple_pushpin,
      red,
      red_small,
      red_dot,
      red_pushpin,
      red_big_stop,
      black_small,
      white_small,
   }

   public class GMarkerGoogle : GMapMarker
   {
      public float? Bearing;
      Bitmap Bitmap;
      Bitmap BitmapShadow;

      public GMarkerGoogle(PointLatLng p, GoogleMarkerType type)
         : base(p)
      {
         Bitmap = GetIcon(type.ToString());
         Size = new System.Drawing.Size(Bitmap.Width, Bitmap.Height);

         switch(type)
         {
            case GoogleMarkerType.arrow:
            {
               Offset = new Point(-11, -Size.Height);
               BitmapShadow = Resources.arrowshadow;
            }
            break;

            case GoogleMarkerType.blue:
            case GoogleMarkerType.blue_dot:
            case GoogleMarkerType.green:
            case GoogleMarkerType.green_dot:
            case GoogleMarkerType.yellow:
            case GoogleMarkerType.yellow_dot:
            case GoogleMarkerType.lightblue:
            case GoogleMarkerType.lightblue_dot:
            case GoogleMarkerType.orange:
            case GoogleMarkerType.orange_dot:
            case GoogleMarkerType.pink:
            case GoogleMarkerType.pink_dot:
            case GoogleMarkerType.purple:
            case GoogleMarkerType.purple_dot:
            case GoogleMarkerType.red:
            case GoogleMarkerType.red_dot:
            {
               Offset = new Point(-Size.Width / 2 + 1, -Size.Height + 1);
               BitmapShadow = Resources.msmarker_shadow;
            }
            break;

            case GoogleMarkerType.black_small:
            case GoogleMarkerType.blue_small:
            case GoogleMarkerType.brown_small:
            case GoogleMarkerType.gray_small:
            case GoogleMarkerType.green_small:
            case GoogleMarkerType.yellow_small:
            case GoogleMarkerType.orange_small:
            case GoogleMarkerType.purple_small:
            case GoogleMarkerType.red_small:
            case GoogleMarkerType.white_small:
            {
               Offset = new Point(-Size.Width / 2, -Size.Height + 1);
               BitmapShadow = Resources.shadow_small;
            }
            break;

            case GoogleMarkerType.green_big_go:
            case GoogleMarkerType.yellow_big_pause:
            case GoogleMarkerType.red_big_stop:
            {
               Offset = new Point(-Size.Width / 2, -Size.Height + 1);
               BitmapShadow = Resources.msmarker_shadow;
            }
            break;

            case GoogleMarkerType.blue_pushpin:
            case GoogleMarkerType.green_pushpin:
            case GoogleMarkerType.yellow_pushpin:
            case GoogleMarkerType.lightblue_pushpin:
            case GoogleMarkerType.pink_pushpin:
            case GoogleMarkerType.purple_pushpin:
            case GoogleMarkerType.red_pushpin:
            {
               Offset = new Point(-9, -Size.Height + 1);
               BitmapShadow = Resources.pushpin_shadow;
            }
            break;
         }
      }

      public GMarkerGoogle(PointLatLng p, Bitmap Bitmap)
         : base(p)
      {
         this.Bitmap = Bitmap;
         Size = new System.Drawing.Size(Bitmap.Width, Bitmap.Height);
         Offset = new Point(-Size.Width / 2, -Size.Height);
      }

      internal static Bitmap GetIcon(string name)
      {
         object obj = Resources.ResourceManager.GetObject(name, Resources.Culture);
         return ((Bitmap) (obj));
      }

      static readonly Point[] Arrow = new Point[] { new Point(-7, 7), new Point(0, -22), new Point(7, 7), new Point(0, 2) };

      public override void OnRender(Graphics g)
      {
#if !PocketPC
         if(!Bearing.HasValue)
         {
            if(BitmapShadow != null)
            {
               g.DrawImage(BitmapShadow, TopLeft.X, TopLeft.Y, BitmapShadow.Width, BitmapShadow.Height);
            }
         }

         if(Bearing.HasValue)
         {
            g.RotateTransform(Bearing.Value - Overlay.Control.Bearing);
            g.FillPolygon(Brushes.Red, Arrow);
         }

         if(!Bearing.HasValue)
         {
            g.DrawImage(Bitmap, TopLeft.X, TopLeft.Y, Size.Width, Size.Height);
         }
#else
         if(BitmapShadow != null)
         {
            DrawImageUnscaled(g, BitmapShadow, LocalPosition.X, LocalPosition.Y);
         }
         DrawImageUnscaled(g, Bitmap, LocalPosition.X, LocalPosition.Y);
#endif
      }
   }
}
