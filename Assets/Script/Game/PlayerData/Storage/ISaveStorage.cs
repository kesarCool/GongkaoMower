/// <summary>本地存档读写抽象（可替换为微信云等）。</summary>
public interface ISaveStorage
{
    bool TryLoad(string key, out string json);
    void Save(string key, string json);
    bool TryLoadString(string key, out string value);
    void SaveString(string key, string value);
}
