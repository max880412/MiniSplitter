/*using System;
using System.IO;
using System.Threading.Tasks;
using WTelegram;
using TL;

public class TelegramMTProtoClient
{
    private readonly Client _client;

    public TelegramMTProtoClient()
    {
        _client = new Client(Config);
    }

    // Método de configuración requerido por WTelegramClient
    private static string Config(string what)
    {
        switch (what)
        {
            case "api_id": return "YOUR_API_ID"; // Reemplaza con tu api_id
            case "api_hash": return "YOUR_API_HASH"; // Reemplaza con tu api_hash
            case "phone_number": return "YOUR_PHONE_NUMBER"; // Reemplaza con tu número de teléfono
            case "session_pathname": return "user.session"; // Ruta para guardar la sesión
            default: return null;
        }
    }

    // Método para iniciar sesión en Telegram
    public async Task AuthenticateAsync()
    {
        var user = await _client.LoginUserIfNeeded(); // Devuelve un objeto User
        if (user != null)
        {
            string name = user.username ?? $"{user.first_name} {user.last_name}".Trim();
            Console.WriteLine($"Autenticado como: {name} (ID: {user.id})");
        }
        else
        {
            Console.WriteLine("No se pudo autenticar el usuario.");
        }
    }

    // Método para enviar un mensaje de texto
    public async Task SendMessageAsync(string username, string message)
    {
        var peer = await GetInputPeerAsync(username);
        if (peer != null)
        {
            long randomId = GenerateRandomLong();
            await _client.Messages_SendMessage(peer, message, randomId);
            Console.WriteLine($"Mensaje enviado a {username}");
        }
    }

    // Método para enviar una imagen con caption
    public async Task SendPhotoAsync(string username, string filePath, string caption = "")
    {
        var peer = await GetInputPeerAsync(username);
        if (peer != null && File.Exists(filePath))
        {
            using (var fileStream = File.OpenRead(filePath))
            {
                var fileName = Path.GetFileName(filePath);
                var mediaInput = await _client.UploadFileAsync(fileName, fileStream);
                var media = new InputMediaUploadedPhoto { file = mediaInput };

                long randomId = GenerateRandomLong();
                await _client.Messages_SendMedia(peer, randomId, media, caption);
                Console.WriteLine($"Imagen enviada a {username} con caption: {caption}");
            }
        }
        else
        {
            Console.WriteLine("El archivo no existe o no se pudo encontrar al usuario.");
        }
    }

    // Método para enviar un video con caption
    public async Task SendVideoAsync(string username, string filePath, string caption = "")
    {
        var peer = await GetInputPeerAsync(username);
        if (peer != null && File.Exists(filePath))
        {
            using (var fileStream = File.OpenRead(filePath))
            {
                var fileName = Path.GetFileName(filePath);
                var mediaInput = await _client.UploadFileAsync(fileName, fileStream);

                var attributes = new DocumentAttributeVideo
                {
                    duration = 0, // Duración en segundos (0 si se desconoce)
                    w = 0,        // Ancho del video (0 si se desconoce)
                    h = 0         // Alto del video (0 si se desconoce)
                    // Puedes agregar más atributos si es necesario
                };

                var media = new InputMediaUploadedDocument
                {
                    file = mediaInput,
                    mime_type = "video/mp4",
                    attributes = new DocumentAttribute[] { attributes }
                };

                long randomId = GenerateRandomLong();
                await _client.Messages_SendMedia(peer, randomId, media, caption);
                Console.WriteLine($"Video enviado a {username} con caption: {caption}");
            }
        }
        else
        {
            Console.WriteLine("El archivo no existe o no se pudo encontrar al usuario.");
        }
    }

    // Método para enviar un mensaje de voz
    public async Task SendVoiceAsync(string username, string filePath)
    {
        var peer = await GetInputPeerAsync(username);
        if (peer != null && File.Exists(filePath))
        {
            using (var fileStream = File.OpenRead(filePath))
            {
                var fileName = Path.GetFileName(filePath);
                var mediaInput = await _client.UploadFileAsync(fileName, fileStream);

                var attributes = new DocumentAttributeAudio
                {
                    duration = 0,      // Duración en segundos
                    is_voice = true    // Indica que es un mensaje de voz
                };

                var media = new InputMediaUploadedDocument
                {
                    file = mediaInput,
                    mime_type = "audio/ogg",
                    attributes = new DocumentAttribute[] { attributes }
                };

                long randomId = GenerateRandomLong();
                await _client.Messages_SendMedia(peer, randomId, media, "");
                Console.WriteLine($"Mensaje de voz enviado a {username}");
            }
        }
        else
        {
            Console.WriteLine("El archivo no existe o no se pudo encontrar al usuario.");
        }
    }

    // Método para enviar una nota de video
    public async Task SendVideoNoteAsync(string username, string filePath)
    {
        var peer = await GetInputPeerAsync(username);
        if (peer != null && File.Exists(filePath))
        {
            using (var fileStream = File.OpenRead(filePath))
            {
                var fileName = Path.GetFileName(filePath);
                var mediaInput = await _client.UploadFileAsync(fileName, fileStream);

                var attributes = new DocumentAttribut
                {
                    duration = 0,            // Duración en segundos
                    w = 384,                 // Ancho (usualmente 384 para notas de video)
                    h = 384,                 // Alto
                    flags[0] = true           // Indica que es una nota de video
                };

                var media = new InputMediaUploadedDocument
                {
                    file = mediaInput,
                    mime_type = "video/mp4",
                    attributes = new DocumentAttribute[] { attributes }
                };

                long randomId = GenerateRandomLong();
                await _client.Messages_SendMedia(peer, randomId, media, "");
                Console.WriteLine($"Nota de video enviada a {username}");
            }
        }
        else
        {
            Console.WriteLine("El archivo no existe o no se pudo encontrar al usuario.");
        }
    }

    // Método auxiliar para obtener un InputPeer a partir del username
    private async Task<InputPeer> GetInputPeerAsync(string username)
    {
        var result = await _client.Contacts_ResolveUsername(username);
        if (result != null && result.peer is PeerUser peerUser)
        {
            var user = (User)result.users[peerUser.user_id];
            return new InputPeerUser { user_id = user.id, access_hash = user.access_hash };
        }

        Console.WriteLine($"No se encontró el usuario '{username}'.");
        return null;
    }

    // Método auxiliar para generar un random_id único
    private long GenerateRandomLong()
    {
        var buffer = new byte[8];
        using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
        {
            rng.GetBytes(buffer);
        }
        return BitConverter.ToInt64(buffer, 0);
    }
}
*/