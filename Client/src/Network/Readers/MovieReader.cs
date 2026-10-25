namespace LibreKO.Network.Readers;

internal sealed class MovieReader : IPacketReader
{
    private const byte SubMovie = 2;
    private const byte Shown = 1;
    private const int IgnoredBytes = 4;

    private readonly MovieSession movie;

    public MovieReader(MovieSession movie) => this.movie = movie;

    public void Register(PacketDispatcher dispatcher) => dispatcher.On(GameOpcodes.GS_UTC_MOVIE, Handle);

    private void Handle(Packet p)
    {
        if (p.RemainingBytes < 2 + IgnoredBytes + 4 || p.ReadByte() != SubMovie || p.ReadByte() != Shown) return;
        for (int i = 0; i < IgnoredBytes; i++) p.ReadByte();
        movie.RaiseMovie(p.ReadInt());
    }
}
