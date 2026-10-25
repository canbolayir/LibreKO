namespace LibreKO.Session;

public sealed class MovieSession
{
    public event Action<int>? MovieEvent;

    internal void RaiseMovie(int movieId) => MovieEvent?.Invoke(movieId);
}
