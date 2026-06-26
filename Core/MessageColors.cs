using System.Windows.Media;
using MESInsight.Core;
using SkiaSharp;

namespace MESInsight.Core
{
    public static class MessageColors
    {
        public static Color Get(MessageType t)
        {
            switch (t)
            {
                case MessageType.UNIT_INFO:           return Color.FromRgb(255, 160,  30);
                case MessageType.NEXT_OPERATION:      return Color.FromRgb(  0, 212, 170);
                case MessageType.UNIT_CHECKIN:        return Color.FromRgb( 63, 185,  80);
                case MessageType.UNIT_RESULT:         return Color.FromRgb(168,  85, 247);
                case MessageType.LOAD_MATERIAL:       return Color.FromRgb(255,  70, 130);
                case MessageType.REQ_MATERIAL_INFO:   return Color.FromRgb(  0, 212, 170);
                case MessageType.REQ_SETUP_CHANGE2:   return Color.FromRgb(255, 220,  40);
                case MessageType.SEMI_VALIDATION2:    return Color.FromRgb( 56, 182, 255);
                case MessageType.REQ_LOADED_MATERIAL: return Color.FromRgb(200, 230,   0);
                default:                              return Color.FromRgb(120, 130, 140);
            }
        }

        public static SKColor GetSki(MessageType t)
        {
            var c = Get(t);
            return new SKColor(c.R, c.G, c.B);
        }
    }
}