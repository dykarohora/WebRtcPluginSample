using System;
using System.Collections.Generic;
using System.Text;

namespace WebRtcPluginSample.Utilities
{
    internal class Resolution
    {
        public uint Width { get; }
        public uint Height { get; }

        public Resolution(uint width, uint height)
        {
            Width = width;
            Height = height;
        }

        public override string ToString()
        {
            return Width + " x " + Height;
        }

        public override bool Equals(object obj)
        {
            Resolution target = obj as Resolution;
            return (Width == target.Width) && (Height == target.Height);
        }
    }
}
