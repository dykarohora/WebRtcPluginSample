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

        public override int GetHashCode()
        {
            var hashCode = 859600377;
            hashCode = hashCode * -1521134295 + Width.GetHashCode();
            hashCode = hashCode * -1521134295 + Height.GetHashCode();
            return hashCode;
        }
    }
}
