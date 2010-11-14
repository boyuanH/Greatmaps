
namespace GMap.NET.WindowsForms
{
   using System;
   using System.Drawing;
   using System.Runtime.Serialization;
   using System.Windows.Forms;
   using GMap.NET.WindowsForms.ToolTips;

   /// <summary>
   /// GMap.NET marker
   /// </summary>
   [Serializable]
#if !PocketPC
   public class GMapMarker : ISerializable
#else
   public class GMapMarker
#endif
   {
#if PocketPC
      static readonly System.Drawing.Imaging.ImageAttributes attr = new System.Drawing.Imaging.ImageAttributes();

      static GMapMarker()
      {
         attr.SetColorKey(Color.White, Color.White);
      }
#endif
      GMapOverlay overlay;
      public GMapOverlay Overlay
      {
         get
         {
            return overlay;
         }
         internal set
         {
            overlay = value;
         }
      }

      private PointLatLng position;
      public PointLatLng Position
      {
         get
         {
            return position;
         }
         set
         {
            if(position != value)
            {
               position = value;

               if(IsVisible)
               {
                  if(Overlay != null && Overlay.Control != null)
                  {
                     var p = Overlay.Control.FromLatLngToLocal(Position);
                     LocalOrigin = new Point(p.X, p.Y);

                     Overlay.Control.ThreadSafeInvalidation();
                  }
               }
            }
         }
      }

      Point localOrigin;

      /// <summary>
      /// local position of the origin of the marker
      /// </summary>
      public Point LocalOrigin
      {
         get
         {
            return localOrigin;
         }
         set
         {
            if(localOrigin != value)
            {
               localOrigin = value;

               var t = localOrigin;
               t.Offset(Offset.X, Offset.Y);
               area.Location = t;

               if(Overlay != null && Overlay.Control != null)
               {
                  if(IsVisible && !Overlay.Control.HoldInvalidation)
                  {
                     Overlay.Control.ThreadSafeInvalidation();
                  }
               }
            }
         }
      }

      /// <summary>
      /// updates position using local position of the origin of the marker
      /// use this if you change LocalOrigin manualy
      /// </summary>
      public void UpdatePositionUsingLocalOrigin()
      {
         if(Overlay != null && Overlay.Control != null)
         {
            position = Overlay.Control.Projection.FromPixelToLatLng(new GPoint(localOrigin.X, localOrigin.Y), Overlay.Control.ZoomStep, true);
         }
      }

      /// <summary>
      /// local position of the top left corner of the marker
      /// </summary>
      public Point TopLeft
      {
         get
         {
            return area.Location;
         }
      }

      /// <summary>
      /// virtual size of th marker
      /// </summary>
      public Size Size
      {
         get
         {
            return area.Size;
         }
         set
         {
            area.Size = value;
         }
      }

      Rectangle area;

      /// <summary>
      /// virtual area of the marker
      /// </summary>
      public Rectangle LocalArea
      {
         get
         {
            return area;
         }
      }

      Point offset;

      /// <summary>
      /// virtual offset in px from real coordinate center
      /// </summary>
      public Point Offset
      {
         get
         {
            return offset;
         }
         set
         {
            offset = value;
         }
      }

      Point toffset;

      /// <summary>
      /// virtual offset of tooltip in px from real coordinate center
      /// </summary>
      public Point ToolTipOffset
      {
         get
         {
            return toffset;
         }
         set
         {
            toffset = value;
         }
      }

      /// <summary>
      /// ToolTip position in local coordinates
      /// </summary>
      public Point ToolTipOrigin
      {
         get
         {
            var ret = localOrigin;
            ret.Offset(toffset);
            return ret;
         }
      }

      public object Tag;
      public GMapToolTip ToolTip;
      public MarkerTooltipMode ToolTipMode = MarkerTooltipMode.OnMouseOver;

      string toolTipText;
      public string ToolTipText
      {
         get
         {
            return toolTipText;
         }

         set
         {
            if(ToolTip == null)
            {
#if !PocketPC
               ToolTip = new GMapRoundedToolTip(this);
#else
               ToolTip = new GMapToolTip(this);
#endif
            }
            toolTipText = value;
         }
      }

      private bool visible = true;

      /// <summary>
      /// is marker visible
      /// </summary>
      public bool IsVisible
      {
         get
         {
            return visible;
         }
         set
         {
            if(value != visible)
            {
               visible = value;

               if(Overlay != null && Overlay.Control != null)
               {
                  if(visible)
                  {
                     Overlay.Control.UpdateMarkerLocalPosition(this);
                  }

                  if(!Overlay.Control.HoldInvalidation)
                  {
                     Overlay.Control.Invalidate();
                  }
               }
            }
         }
      }

      /// <summary>
      /// if true, marker will be rendered even if it's outside current view
      /// </summary>
      public bool DisableRegionCheck = false;

      /// <summary>
      /// can maker receive input
      /// </summary>
      public bool IsHitTestVisible = true;

      private bool isMouseOver = false;

      /// <summary>
      /// is mouse over marker
      /// </summary>
      public bool IsMouseOver
      {
         get
         {
            return isMouseOver;
         }
         internal set
         {
            isMouseOver = value;
            Overlay.Control.IsMouseOverMarker = value;
         }
      }

      public GMapMarker(PointLatLng pos)
      {
         this.Position = pos;
      }

      public virtual void OnRender(Graphics g)
      {
         //
      }

#if PocketPC
      protected void DrawImageUnscaled(Graphics g, Bitmap inBmp, int x, int y)
      {
         g.DrawImage(inBmp, new Rectangle(x, y, inBmp.Width, inBmp.Height), 0, 0, inBmp.Width, inBmp.Height, GraphicsUnit.Pixel, attr);
      }
#endif

#if !PocketPC
      #region ISerializable Members

      /// <summary>
      /// Populates a <see cref="T:System.Runtime.Serialization.SerializationInfo"/> with the data needed to serialize the target object.
      /// </summary>
      /// <param name="info">The <see cref="T:System.Runtime.Serialization.SerializationInfo"/> to populate with data.</param>
      /// <param name="context">The destination (see <see cref="T:System.Runtime.Serialization.StreamingContext"/>) for this serialization.</param>
      /// <exception cref="T:System.Security.SecurityException">
      /// The caller does not have the required permission.
      /// </exception>
      public void GetObjectData(SerializationInfo info, StreamingContext context)
      {
         info.AddValue("Position", this.Position);
         info.AddValue("Tag", this.Tag);
         info.AddValue("Offset", this.Offset);
         info.AddValue("Area", this.area);
         info.AddValue("ToolTip", this.ToolTip);
         info.AddValue("ToolTipMode", this.ToolTipMode);
         info.AddValue("ToolTipText", this.ToolTipText);
         info.AddValue("Visible", this.IsVisible);
         info.AddValue("DisableregionCheck", this.DisableRegionCheck);
         info.AddValue("IsHitTestVisible", this.IsHitTestVisible);
      }

      /// <summary>
      /// Initializes a new instance of the <see cref="GMapMarker"/> class.
      /// </summary>
      /// <param name="info">The info.</param>
      /// <param name="context">The context.</param>
      protected GMapMarker(SerializationInfo info, StreamingContext context)
      {
         this.Position = info.GetStruct<PointLatLng>("Position", PointLatLng.Zero);
         this.Tag = info.GetValue<object>("Tag", null);
         this.Offset = info.GetStruct<Point>("Offset", Point.Empty);
         this.area = info.GetStruct<Rectangle>("Area", Rectangle.Empty);
         this.ToolTip = info.GetValue<GMapToolTip>("ToolTip", null);
         this.ToolTipMode = info.GetStruct<MarkerTooltipMode>("ToolTipMode", MarkerTooltipMode.OnMouseOver);
         this.ToolTipText = info.GetString("ToolTipText");
         this.IsVisible = info.GetBoolean("Visible");
         this.DisableRegionCheck = info.GetBoolean("DisableregionCheck");
         this.IsHitTestVisible = info.GetBoolean("IsHitTestVisible");
      }

      #endregion
#endif
   }

   public delegate void MarkerClick(GMapMarker item, MouseEventArgs e);
   public delegate void MarkerEnter(GMapMarker item);
   public delegate void MarkerLeave(GMapMarker item);

   /// <summary>
   /// modeof tooltip
   /// </summary>
   public enum MarkerTooltipMode
   {
      OnMouseOver,
      Never,
      Always,
   }
}
