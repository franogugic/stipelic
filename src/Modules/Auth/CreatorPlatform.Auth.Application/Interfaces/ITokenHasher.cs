namespace CreatorPlatform.Auth.Application.Interfaces;

public interface ITokenHasher
{
    string Hash(string token);
}
