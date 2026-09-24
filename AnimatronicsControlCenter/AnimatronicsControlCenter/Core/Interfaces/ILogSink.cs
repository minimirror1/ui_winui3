using System;

namespace AnimatronicsControlCenter.Core.Interfaces
{
    /// 기록 한 줄이 실제로 나가는 곳. 날짜가 바뀌거나 파일이 상한에 닿으면 넘기는 일도 여기서 맡는다.
    public interface ILogSink : IDisposable
    {
        void Write(DateOnly date, string line);

        void Flush();
    }
}
