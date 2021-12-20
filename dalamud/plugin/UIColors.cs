using System;

namespace HarpHero
{
    public class UIColors
    {
        // palette used for drawing notes and stuff
        public const uint colorGreen = 0xff09ed88;
        public const uint colorGreenDark = 0xff09be88;

        public const uint colorYellow = 0xff40cbf9;
        public const uint colorYellowDark = 0xff07b4ed;

        public const uint colorRed = 0xff004aff;
        public const uint colorRedDark = 0xff0a2bff;

        public const uint colorPink = 0xffff61ff;
        public const uint colorPinkDark = 0xffff2fff;

        public const uint colorBlue = 0xffff6d00;
        public const uint colorBlueDark = 0xffff3a00;

        public const uint colorGrayGreen = 0xff70a890;
        public const uint colorGrayGreenDark = 0xff3a564a;

        public const uint colorGrayYellow = 0xff8dc2d3;
        public const uint colorGrayYellowDark = 0xff5a7c87;

        public const uint colorGrayRed = 0xff4b69b7;
        public const uint colorGrayRedDark = 0xff374e87;

        public const uint colorGrayPink = 0xffc695c6;
        public const uint colorGrayPinkDark = 0xff916d91;

        public const uint colorGrayBlue = 0xffc68655;
        public const uint colorGrayBlueDark = 0xff875b3a;

        public const uint colorGray25 = 0xff404040;
        public const uint colorGray33 = 0xff545454;
        public const uint colorGray50 = 0xff808080;

        // pallette assignments
        public const uint colorXboxActionNorth = colorYellow;
        public const uint colorXboxActionNorthDark = colorYellowDark;
        public const uint colorXboxActionSouth = colorGreen;
        public const uint colorXboxActionSouthDark = colorGreenDark;
        public const uint colorXboxActionEast = colorRed;
        public const uint colorXboxActionEastDark = colorRedDark;
        public const uint colorXboxActionWest = colorBlue;
        public const uint colorXboxActionWestDark = colorBlueDark;

        public const uint colorXboxDPadNorth = colorGrayYellow;
        public const uint colorXboxDPadNorthDark = colorGrayYellowDark;
        public const uint colorXboxDPadSouth = colorGrayGreen;
        public const uint colorXboxDPadSouthDark = colorGrayGreenDark;
        public const uint colorXboxDPadEast = colorGrayRed;
        public const uint colorXboxDPadEastDark = colorGrayRedDark;
        public const uint colorXboxDPadWest = colorGrayBlue;
        public const uint colorXboxDPadWestDark = colorGrayBlueDark;

        public const uint colorSonyActionNorth = colorGreen;
        public const uint colorSonyActionNorthDark = colorGreenDark;
        public const uint colorSonyActionSouth = colorBlue;
        public const uint colorSonyActionSouthDark = colorBlueDark;
        public const uint colorSonyActionEast = colorRed;
        public const uint colorSonyActionEastDark = colorRedDark;
        public const uint colorSonyActionWest = colorPink;
        public const uint colorSonyActionWestDark = colorPinkDark;

        public const uint colorSonyDPadNorth = colorGrayGreen;
        public const uint colorSonyDPadNorthDark = colorGrayGreenDark;
        public const uint colorSonyDPadSouth = colorGrayBlue;
        public const uint colorSonyDPadSouthDark = colorGrayBlueDark;
        public const uint colorSonyDPadEast = colorGrayRed;
        public const uint colorSonyDPadEastDark = colorGrayRedDark;
        public const uint colorSonyDPadWest = colorGrayPink;
        public const uint colorSonyDPadWestDark = colorGrayPinkDark;

        public static readonly uint[] colorKeyboardNotes = { colorGreen, colorYellow, colorRed, colorPink, colorYellow, colorGreen };
        public static readonly uint[] colorKeyboardNotesDark = { colorGreenDark, colorYellowDark, colorRedDark, colorPinkDark, colorYellowDark, colorGreenDark };

        public static uint GetAlphaModulated(uint color, float alphaScale = 1.0f)
        {
            var modAlpha = ((color >> 24) / 255.0f) * alphaScale;
            return (color & 0x00ffffff) | (uint)Math.Min(255, modAlpha * 255) << 24;
        }
    }
}
