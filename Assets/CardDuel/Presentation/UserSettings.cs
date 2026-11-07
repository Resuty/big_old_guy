using System;

namespace CardDuel.Presentation
{
    [Serializable]
    public sealed class UserSettings
    {
        public int Language, Difficulty=1, Resolution=2;
        public float Music=.35f, Sound=.65f;
        public bool Fast;
        public void Sanitize()
        {
            Language=Math.Max(0,Math.Min(2,Language));Difficulty=Math.Max(0,Math.Min(1,Difficulty));
            Resolution=Math.Max(0,Math.Min(4,Resolution));Music=Volume(Music,.35f);Sound=Volume(Sound,.65f);
        }
        static float Volume(float value,float fallback) => float.IsNaN(value)||float.IsInfinity(value)?fallback:Math.Max(0,Math.Min(1,value));
    }
}
