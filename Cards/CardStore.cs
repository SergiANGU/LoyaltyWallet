using System.Collections.Concurrent;

namespace LoyaltyWallet.Cards;

public sealed record Member(string Id, string Name, int Points);

/// <summary>
/// Almacén en memoria para el test. Al reiniciar se pierde; en un proyecto real sería EF Core + SQL Server.
/// </summary>
public sealed class CardStore
{
    private readonly ConcurrentDictionary<string, Member> _members = new();

    public Member Create(string name)
    {
        // Prefijo + GUID corto: los IDs de Google solo admiten letras, números, '.', '_' y '-'
        var member = new Member($"socio-{Guid.NewGuid():N}"[..18], name, 0);
        _members[member.Id] = member;
        return member;
    }

    public Member? Get(string id) => _members.GetValueOrDefault(id);

    public IReadOnlyCollection<Member> All() => _members.Values.ToList();

    public Member AddPoints(Member member, int amount)
    {
        var updated = member with { Points = Math.Max(0, member.Points + amount) };
        _members[member.Id] = updated;
        return updated;
    }
}
