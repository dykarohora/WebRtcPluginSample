using System;
using System.Collections.Generic;
using System.Text;

namespace WebRtcPluginSample.Utilities
{
    internal class Resolution
    {
        public int Width { get; set; }
        public int Height { get; set; }

        public Resolution(int width, int height)
        {
            Width = width;
            Height = height;
        }

        public override string ToString()
        {
            return Width + " x " + Height;
        }
    }
}