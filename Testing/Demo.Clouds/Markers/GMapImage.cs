
using System.Drawing;
using GMap.NET.WindowsForms;
using GMap.NET;

namespace CloudsDemo
{
   public class GMapImage : GMapMarker
   {
      private Image image;
      public Image Image
      {
         get
         {
            return image;
         }
         set
         {
            image = value;
            if(image != null)
            {
               this.Size = new Size(image.Width, image.Height);
            }
         }
      }

      public GMapImage(PointLatLng p)
         : base(p)
      {
         DisableRegionCheck = true;
         IsHitTestVisible = false;
      }

      public override void OnRender(Graphics g)
      {
         if(image == null)
            return;

         g.DrawImage(image, TopLeft.X, TopLeft.Y, Size.Width, Size.Height);
      }
   }
}