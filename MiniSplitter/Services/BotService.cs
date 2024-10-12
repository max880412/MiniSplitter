using System;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using MiniSplitter.Data;
using System.Threading;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MiniSplitter.Models;
using Telegram.Bot.Types.ReplyMarkups;
using System.Text;
using System.Globalization;

namespace MiniSplitter.Services
{
    public class BotService
    {
        private readonly ITelegramBotClient botClient;
        private readonly string botToken;
        private readonly long adminGroupId;
        private readonly int generalTopicId;
        private readonly int commandsTopicId;
        private readonly int reportsTopicId;
        private readonly int remindersTopicId;
        private readonly int chanPubsTopicId;

        //private readonly string welcomeImagePath;
        //private readonly string welcomeText;

        private readonly Dictionary<string, string> reminderImages;
        private readonly Dictionary<string, string> reminderTexts;

        private Dictionary<long, ReminderState> operatorReminderStates = new Dictionary<long, ReminderState>();

        private class ReminderState
        {
            public string ReminderText { get; set; }
            public string MediaType { get; set; } // "text", "photo", "video", "audio", "document"
            public string MediaFilePath { get; set; }
            public DateTime? ScheduledTime { get; set; }
            public bool WaitingForTime { get; set; }
        }

        public BotService()
        {
            // Cargar configuración
            botToken = "7746921465:AAGLrcueT7Q8JHd-h-F07wZd3P8OMk6bOJU";
            botClient = new TelegramBotClient(botToken);

            adminGroupId = -1002424181604; // Reemplaza con el ID de tu grupo de administración
            generalTopicId = 0; // Reemplaza con el ID del tema General
            commandsTopicId = 6; // Reemplaza con el ID del tema Commands
            reportsTopicId = 2; // Reemplaza con el ID del tema Reports
            remindersTopicId = 4; // Reemplaza con el ID del tema Reminders
            chanPubsTopicId = 90; //Remplazar con el ID del tema Publicar

            
            // Rutas a las imágenes y textos de los recordatorios
            reminderImages = new Dictionary<string, string>
            {
                { "30min", Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "images", "reminder_30min.jpg") },
                { "1hour", Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "images", "reminder_1hour.jpg") },
                { "2hours", Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "images", "reminder_2hours.jpg") }
            };

            reminderTexts = new Dictionary<string, string>
            {
                { "30min", "Recordatorio después de 30 minutos." },
                { "1hour", "Recordatorio después de 1 hora." },
                { "2hours", "Recordatorio después de 2 horas." }
            };

            // Inicializar la base de datos
            Database.Initialize();

            // Iniciar recepción de actualizaciones
            CancellationTokenSource cts = new CancellationTokenSource();

            var receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = new[] { UpdateType.Message, UpdateType.ChatJoinRequest, UpdateType.CallbackQuery }
            };

            botClient.StartReceiving(
                updateHandler: HandleUpdateAsync,
                pollingErrorHandler: HandlePollingErrorAsync,
                receiverOptions: receiverOptions,
                cancellationToken: cts.Token
            );

            Console.WriteLine($"Bot iniciado con el nombre {botClient.GetMeAsync().Result.Username}");

            // Iniciar tareas periódicas
            StartPeriodicTasks();
        }

        private void StartPeriodicTasks()
        {
            // Programar la generación de reportes diarios a medianoche
            var now = DateTime.Now;
            var nextMidnight = now.Date.AddDays(1);
            var timeUntilMidnight = nextMidnight - now;

            var timer = new System.Timers.Timer(timeUntilMidnight.TotalMilliseconds);
            timer.Elapsed += (sender, e) =>
            {
                GenerateDailyReport();
                timer.Interval = TimeSpan.FromDays(1).TotalMilliseconds; // Subsecuentemente, cada 24 horas
            };
            timer.AutoReset = true;
            timer.Start();
        }

        private void GenerateDailyReport()
        {
            // Obtener datos para el reporte
            var totalClients = Database.GetAllActiveClients().Count;
            var totalOperators = Database.GetAllOperators().Count(o => o.IsActive);

            var report = new Report
            {
                ReportDate = DateTime.Now.Date,
                TotalClients = totalClients,
                TotalOperators = totalOperators
            };

            Database.AddReport(report);

            // Enviar reporte al grupo de administración
            var reportText = $"📊 *Reporte Diario*\n\n" +
                             $"Fecha: {report.ReportDate:dd/MM/yyyy}\n" +
                             $"Total de Clientes Activos: {report.TotalClients}\n" +
                             $"Total de Operadores Activos: {report.TotalOperators}";

            botClient.SendTextMessageAsync(
                chatId: adminGroupId,
                text: reportText,
                parseMode: ParseMode.Markdown,
                messageThreadId: reportsTopicId
            ).Wait();
        }

        private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            try
            {
                if (update.Type == UpdateType.ChatJoinRequest)
                {
                    await HandleChatJoinRequest(update.ChatJoinRequest);
                }
                else if (update.Type == UpdateType.Message)
                {
                    var message = update.Message;
                    if (message.Chat.Type == ChatType.Private)
                    {
                        await HandleClientMessage(message);
                    }
                    else if (message.Chat.Id == adminGroupId)
                    {
                        await HandleAdminGroupMessage(message);
                    }
                }
                else if (update.Type == UpdateType.CallbackQuery)
                {
                    await HandleCallbackQuery(update.CallbackQuery);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en HandleUpdateAsync: {ex.Message}");
                await SendErrorToGeneral($"Error en HandleUpdateAsync: {ex.Message}");
            }
        }

        private Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            var errorMessage = exception switch
            {
                ApiRequestException apiEx => $"Error de la API de Telegram: {apiEx.Message}",
                _ => exception.Message
            };
            Console.WriteLine($"Error en HandlePollingErrorAsync: {errorMessage}");
            return SendErrorToGeneral($"Error en HandlePollingErrorAsync: {errorMessage}");
        }

        private async Task HandleChatJoinRequest(ChatJoinRequest chatJoinRequest)
        {
            try
            {
                var user = chatJoinRequest.From;
                var channelId = chatJoinRequest.Chat.Id;

                // Verificar si el canal está en la base de datos y está activo
                var channel = Database.GetChannelByTelegramId(channelId);
                if (channel == null || !channel.IsActive)
                {
                    // Rechazar la solicitud
                    await botClient.DeclineChatJoinRequest(channelId, user.Id);
                    return;
                }

                // Verificar si hay al menos un operador activo en el canal
                var operatorAssigned = Database.GetOperatorWithLeastClients(channelId);
                if (operatorAssigned == null)
                {
                    // Rechazar la solicitud
                    await botClient.DeclineChatJoinRequest(channelId, user.Id);
                    // Notificar al grupo de administración
                    await SendMessageToGeneral($"No hay operadores activos disponibles para el canal {channel.ChanName}.");
                    return;
                }

                // Aprobar la solicitud
                await botClient.ApproveChatJoinRequest(channelId, user.Id);

                // Asignar operador
                operatorAssigned.AssignedClientsToday += 1;
                Database.UpdateOperator(operatorAssigned);

                // Crear registro del cliente
                var client = new Client
                {
                    ClientId = user.Id,
                    ClientName = user.Username,
                    ClientFirstName = user.FirstName,
                    ClientChannelId = channelId,
                    ClientOperatorId = operatorAssigned.OpId,
                    IsActive = true,
                    ClientWrote = false,
                    ClientEntryDate = DateTime.Now
                };

                // Crear thread en el grupo de administración para comunicación
                var threadName = $"{user.Username ?? user.FirstName} - {channel.ChanName}";

                var forumTopic = await botClient.CreateForumTopicAsync(adminGroupId, threadName);

                client.ClientThreadId = forumTopic.MessageThreadId;
                Database.AddClient(client);

                // Enviar información del cliente al thread
                await SendClientInfoToThread(client, operatorAssigned, channel);

                // Enviar mensaje de bienvenida con el botón
                SetupCommands(user.Id);

                //Mensaje de bienvenida
                var welcomeImagePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "images", "welcome.jpg");
                var welcomeText = $"Hola {user.FirstName} 👋 bienvenido, a mi canal, soy  MASTER en  ARBITRAJE de Criptomonedas 👨🏻‍💻 desde hace 6 años aproximadamente.\r\n\r\nINSTAGRAM : https://bit.ly/3SjLGTZ \r\n\r\n✅ En este canal enseño a ganar dinero ARBITRANDO CRIPTOS a través de BYBIT,  🆙\r\n\r\nTE INTERESA 💬\r\n👇TOCA PARA EMPEZAR👇"; // Texto de bienvenida predefinido

                //keyboard
                var keyboardMarkup = new ReplyKeyboardMarkup(new[]
                {
                    new KeyboardButton[] { "✅EMPEZAR A GANAR🚀" },
                    new KeyboardButton[] {"Cerrar" }
                })

                {
                    ResizeKeyboard = true, // Cambia el tamaño del teclado para que se ajuste mejor a la pantalla.
                    OneTimeKeyboard = true // Mantiene el teclado visible hasta que el usuario lo cierre.
                };

                using (var fileStream = new FileStream(welcomeImagePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var inputFile = InputFile.FromStream(fileStream);
                    
                    await botClient.SendPhotoAsync(
                        chatId: client.ClientId,
                        photo: inputFile,
                        caption: welcomeText,
                        replyMarkup: keyboardMarkup
                    );
                }

                //ShowMenu(user.Id);

                // Programar recordatorios automáticos
                //ScheduleReminders(client);

                // Registrar el evento
                await SendMessageToGeneral($"Cliente {client.ClientName ?? client.ClientFirstName} se unió al canal {channel.ChanName} y fue asignado al operador {operatorAssigned.OpUsername}.");

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en HandleChatJoinRequest: {ex.Message}");
                await SendErrorToGeneral($"Error en HandleChatJoinRequest: {ex.Message}");
            }
        }

        private async Task ShowMenu(long chatId)
        {
            var keyboardMarkup = new ReplyKeyboardMarkup(new[]
            {
                new KeyboardButton[] { "✅EMPEZAR A GANAR🚀", "Cerrar" }
            })
            {
                ResizeKeyboard = true, // Cambia el tamaño del teclado para que se ajuste mejor a la pantalla.
                OneTimeKeyboard = true // Mantiene el teclado visible hasta que el usuario lo cierre.
            };

            await botClient.SendTextMessageAsync(
                chatId: chatId,
                text: "Selecciona una opción:",
                replyMarkup: keyboardMarkup
            );
        }
        private async Task HideMenu(long chatId)
        {
            var removeKeyboard = new ReplyKeyboardRemove();

            await botClient.SendTextMessageAsync(
                chatId: chatId,
                text: "Menú oculto.",
                replyMarkup: removeKeyboard
            );
        }


        private async Task SetupCommands(long chatid)
        {
            var commands = new[]
            {
                new BotCommand { Command = "/acepto", Description = "Empezar a ganar!" },
                new BotCommand { Command = "/noacepto", Description = "Cerrar" },
            };

            // Establecer los comandos en el bot
            await botClient.SetMyCommandsAsync(commands);

            // Configurar el botón de menú para estos comandos
            await botClient.SetChatMenuButtonAsync(chatId: chatid, menuButton: new MenuButtonCommands());
        }

        private async Task HideCommands(long chatid)
        {
            await botClient.SetChatMenuButtonAsync(chatId: chatid);

        }

        private async Task SendClientInfoToThread(Client client, Operator operatorAssigned, Channel channel)
        {
            try
            {
                var clientInfo = new StringBuilder();
                clientInfo.AppendLine("📋 *Información del Cliente*");
                clientInfo.AppendLine($"👤 *Nombre:* {client.ClientFirstName}");
                clientInfo.AppendLine($"🔹 *Usuario:* @{client.ClientName ?? "Sin username"}");
                clientInfo.AppendLine($"🕓 *Hora de Entrada:* {client.ClientEntryDate:dd/MM/yyyy HH:mm}");
                clientInfo.AppendLine($"📢 *Canal de Origen:* {channel.ChanName}");
                clientInfo.AppendLine($"👨‍💼 *Operador Asignado:* @{operatorAssigned.OpUsername}");

                // Botón para bloquear al cliente
                var inlineKeyboard = new InlineKeyboardMarkup(new[]
                {
                    InlineKeyboardButton.WithCallbackData("🚫 Bloquear Cliente", $"block_client_{client.ClientId}")
                });

                await botClient.SendTextMessageAsync(
                    chatId: adminGroupId,
                    text: clientInfo.ToString(),
                    parseMode: ParseMode.Markdown,
                    messageThreadId: (int)client.ClientThreadId,
                    replyMarkup: inlineKeyboard
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al enviar información del cliente al thread: {ex.Message}");
                await SendErrorToGeneral($"Error al enviar información del cliente al thread: {ex.Message}");
            }
        }

        private async Task HandleCallbackQuery(CallbackQuery callbackQuery)
        {
            try
            {
                var data = callbackQuery.Data;
                var userId = callbackQuery.From.Id;

                if (data == "start_command")
                {
                    // Obtener cliente de la base de datos
                    var client = Database.GetClientById(userId);
                    if (client == null || !client.IsActive)
                    {
                        return;
                    }

                    // Enviar mensaje de felicitación
                    await botClient.SendTextMessageAsync(
                        chatId: client.ClientId,
                        text: "¡Felicidades! Has completado el primer paso."
                    );

                    // Crear botón con URL al perfil del operador
                    var operatorAssigned = Database.GetOperatorById(client.ClientOperatorId);
                    if (operatorAssigned != null)
                    {
                        var operatorProfileUrl = $"https://t.me/{operatorAssigned.OpUsername}";
                        var inlineKeyboard = new InlineKeyboardMarkup(new[]
                        {
                            InlineKeyboardButton.WithUrl($"Contactar a @{operatorAssigned.OpUsername}", operatorProfileUrl)
                        });

                        // Enviar mensaje con el botón al operador
                        await botClient.SendTextMessageAsync(
                            chatId: client.ClientId,
                            text: "Ahora puedes contactar a tu operador asignado:",
                            replyMarkup: inlineKeyboard
                        );
                    }

                    // Responder al CallbackQuery para eliminar el ícono de "cargando"
                    await botClient.AnswerCallbackQueryAsync(callbackQuery.Id);
                }
                else if (data.StartsWith("block_client_"))
                {
                    // Manejar bloqueo del cliente
                    var clientIdString = data.Replace("block_client_", "");
                    if (long.TryParse(clientIdString, out long clientId))
                    {
                        await BlockClient(clientId, callbackQuery);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en HandleCallbackQuery: {ex.Message}");
                await SendErrorToGeneral($"Error en HandleCallbackQuery: {ex.Message}");
            }
        }

        private async Task BlockClient(long clientId, CallbackQuery callbackQuery)
        {
            try
            {
                // Obtener cliente de la base de datos
                var client = Database.GetClientById(clientId);
                if (client == null)
                {
                    await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "Cliente no encontrado.");
                    return;
                }

                // Desactivar al cliente en la base de datos
                client.IsActive = false;
                Database.UpdateClient(client);

                // Banear al cliente del canal si es posible
                try
                {
                    await botClient.BanChatMemberAsync(
                        chatId: client.ClientChannelId,
                        userId: client.ClientId
                    );

                    await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "Cliente bloqueado y baneado del canal.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error al banear al cliente del canal: {ex.Message}");
                    await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "Cliente bloqueado, pero no se pudo banear del canal.");
                }

                // Notificar en el thread del cliente
                await botClient.SendTextMessageAsync(
                    chatId: adminGroupId,
                    text: "El cliente ha sido bloqueado.",
                    messageThreadId: (int)client.ClientThreadId
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en BlockClient: {ex.Message}");
                await SendErrorToGeneral($"Error en BlockClient: {ex.Message}");
                await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "Error al bloquear al cliente.");
            }
        }

        private async Task HandleClientMessage(Message message)
        {
            try
            {
                // Obtener cliente de la base de datos
                var client = Database.GetClientById(message.From.Id);
                if (client == null || !client.IsActive)
                {
                    return;
                }

                // Actualizar client_wrote
                if (!client.ClientWrote)
                {
                    client.ClientWrote = true;
                    Database.UpdateClient(client);
                }
                if (message.Text.StartsWith("✅EMPEZAR A GANAR🚀"))
                {
                    var button = new InlineKeyboardMarkup(new[]
                    {
                        InlineKeyboardButton.WithUrl("Escribeme ahora!", $"https://t.me/{Database.GetOperatorById(client.ClientOperatorId).OpUsername}")
                    });

                    var urlImg = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "images", "reminder_30min.jpg");

                    var text = $"📲Escríbeme al privado para presentarte mi ✅ MÉTODO 💸." +
                        $" \r\n\r\n📊STAKING + ARBITRAJE" +
                        $" \r\n(100% Legal y Verificado)" +
                        $"\r\n\r\nEstás son mis ganancias" +
                        $" \r\n✅ Ganancia mínima $150 x día" +
                        $" \r\n✅ Más de $250000 en retiros" +
                        $" \r\n✅ + de 8000 seguidores \r\n✅ Afiliación con BYBIT" +
                        $" \r\n\r\n👇👇ESCRÍBEME 🚀👇👇";

                    await botClient.SendPhotoAsync(
                        client.ClientId,
                        caption:text,
                        photo: InputFile.FromStream(new FileStream(path: urlImg, FileMode.Open)),
                        replyMarkup: button
                        );
                }
                else if (message.Text.StartsWith("Cerrar"))
                {
                    var button = new InlineKeyboardMarkup(new[]
                    {
                        InlineKeyboardButton.WithUrl("Escribeme ahora!", $"https://t.me/{Database.GetOperatorById(client.ClientOperatorId)}")
                    });
                    var text = $"Es una pena, {client.ClientFirstName} " +
                                $"espero recapacite y vea el dia de hoy como un punto de inflexion en su vida.";
                    await botClient.SendTextMessageAsync(chatId: client.ClientId, text: text, replyMarkup: button);
                }

                // Reenviar mensaje al thread del operador
                await botClient.CopyMessageAsync(
                    chatId: adminGroupId,
                    fromChatId: message.Chat.Id,
                    messageId: message.MessageId,
                    messageThreadId: (int)client.ClientThreadId
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en HandleClientMessage: {ex.Message}");
                await SendErrorToGeneral($"Error en HandleClientMessage: {ex.Message}");
            }
        }

        private async Task HandleAdminGroupMessage(Message message)
        {
            try
            {
                if (message.MessageThreadId == commandsTopicId)
                {
                    // Manejar comandos
                    await HandleCommand(message);
                }
                else if (message.MessageThreadId == reportsTopicId)
                {
                    // Manejar reportes
                    await HandleReportRequest(message);
                }
                else if (message.MessageThreadId == remindersTopicId)
                {
                    // Manejar recordatorios
                    await HandleReminderMessage(message);
                }
                else if (message.MessageThreadId == generalTopicId)
                {
                    // Mensajes generales o logs
                }
                else
                {
                    // Asumir que es un mensaje para un cliente
                    // Obtener cliente por thread ID
                    var client = Database.GetClientByThreadId(message.MessageThreadId.Value);
                    if (client == null)
                    {
                        return;
                    }

                    // Reenviar mensaje al cliente
                    await botClient.CopyMessageAsync(
                        chatId: client.ClientId,
                        fromChatId: message.Chat.Id,
                        messageId: message.MessageId
                    );
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en HandleAdminGroupMessage: {ex.Message}");
                await SendErrorToGeneral($"Error en HandleAdminGroupMessage: {ex.Message}");
            }
        }

        private async Task HandleCommand(Message message)
        {
            try
            {
                var commandParts = message.Text.Split(' ');
                var command = commandParts[0].ToLower();

                switch (command)
                {
                    case "/addchannel":
                        await AddChannelCommand(commandParts, message);
                        break;
                    case "/addoperator":
                        await AddOperatorCommand(commandParts, message);
                        break;
                    case "/removeoperator":
                        await RemoveOperatorCommand(commandParts, message);
                        break;
                    case "/deactivateuser":
                        await DeactivateUserCommand(commandParts, message);
                        break;
                    // Agregar más comandos según sea necesario
                    default:
                        await botClient.SendTextMessageAsync(
                            chatId: adminGroupId,
                            text: "Comando no reconocido.",
                            messageThreadId: commandsTopicId
                        );
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en HandleCommand: {ex.Message}");
                await SendErrorToGeneral($"Error en HandleCommand: {ex.Message}");
            }
        }

        private async Task AddChannelCommand(string[] commandParts, Message message)
        {
            if (commandParts.Length < 3)
            {
                await botClient.SendTextMessageAsync(
                    chatId: adminGroupId,
                    text: "Uso correcto: /addchannel [ID del Canal] [Nombre del Canal]",
                    messageThreadId: commandsTopicId
                );
                return;
            }

            if (!long.TryParse(commandParts[1], out var channelId))
            {
                await botClient.SendTextMessageAsync(
                    chatId: adminGroupId,
                    text: "ID del canal inválido.",
                    messageThreadId: commandsTopicId
                );
                return;
            }

            var channelName = string.Join(' ', commandParts.Skip(2));

            var channel = new Channel
            {
                ChanId = channelId,
                ChanName = channelName,
                IsActive = true
            };

            Database.AddChannel(channel);

            await botClient.SendTextMessageAsync(
                chatId: adminGroupId,
                text: $"Canal '{channelName}' agregado con éxito.",
                messageThreadId: commandsTopicId
            );
        }

        private async Task AddOperatorCommand(string[] commandParts, Message message)
        {
            if (commandParts.Length < 4)
            {
                await botClient.SendTextMessageAsync(
                    chatId: adminGroupId,
                    text: "Uso correcto: /addoperator [Id del Operador] [Username del Operador] [ID del Canal]",
                    messageThreadId: commandsTopicId
                );
                return;
            }

            if (!long.TryParse(commandParts[1], out var opid))
            {
                await botClient.SendTextMessageAsync(
                    chatId: adminGroupId,
                    text: "ID del usuario inválido.",
                    messageThreadId: commandsTopicId
                );
                return;
            }

            var username = commandParts[2].TrimStart('@');
            if (!long.TryParse(commandParts[3], out var channelId))
            {
                await botClient.SendTextMessageAsync(
                    chatId: adminGroupId,
                    text: "ID del canal inválido.",
                    messageThreadId: commandsTopicId
                );
                return;
            }

            var channel = Database.GetChannelByTelegramId(channelId);
            if (channel == null || !channel.IsActive)
            {
                await botClient.SendTextMessageAsync(
                    chatId: adminGroupId,
                    text: "El canal no existe o no está activo.",
                    messageThreadId: commandsTopicId
                );
                return;
            }

            var op = new Operator
            {
                OpId = opid,
                OpUsername = username,
                OpChannel = channelId,
                IsActive = true,
                AssignedClientsToday = 0
            };

            Database.AddOperator(op);

            await botClient.SendTextMessageAsync(
                chatId: adminGroupId,
                text: $"Operador '{username}' agregado con éxito al canal '{channel.ChanName}'."
                , messageThreadId: commandsTopicId
            );
        }

        private async Task RemoveOperatorCommand(string[] commandParts, Message message)
        {
            if (commandParts.Length < 2)
            {
                await botClient.SendTextMessageAsync(
                    chatId: adminGroupId,
                    text: "Uso correcto: /removeoperator [Username del Operador]",
                    messageThreadId: commandsTopicId
                );
                return;
            }

            var username = commandParts[1].TrimStart('@');

            // Obtener operador por username
            var op = Database.GetAllOperators().FirstOrDefault(o => o.OpUsername == username);

            if (op == null)
            {
                await botClient.SendTextMessageAsync(
                    chatId: adminGroupId,
                    text: $"No se encontró el operador '{username}'.",
                    messageThreadId: commandsTopicId
                );
                return;
            }

            op.IsActive = false;
            Database.UpdateOperator(op);

            await botClient.SendTextMessageAsync(
                chatId: adminGroupId,
                text: $"Operador '{username}' desactivado con éxito.",
                messageThreadId: commandsTopicId
            );
        }

        private async Task DeactivateUserCommand(string[] commandParts, Message message)
        {
            if (commandParts.Length < 2)
            {
                await botClient.SendTextMessageAsync(
                    chatId: adminGroupId,
                    text: "Uso correcto: /deactivateuser [ID del Usuario]",
                    messageThreadId: commandsTopicId
                );
                return;
            }

            if (!long.TryParse(commandParts[1], out var userId))
            {
                await botClient.SendTextMessageAsync(
                    chatId: adminGroupId,
                    text: "ID del usuario inválido.",
                    messageThreadId: commandsTopicId
                );
                return;
            }

            var client = Database.GetClientById(userId);
            if (client == null)
            {
                await botClient.SendTextMessageAsync(
                    chatId: adminGroupId,
                    text: $"No se encontró el usuario con ID {userId}.",
                    messageThreadId: commandsTopicId
                );
                return;
            }

            // Desactivar al cliente en la base de datos
            client.IsActive = false;
            Database.UpdateClient(client);

            // Banear al cliente del canal si es posible
            try
            {
                await botClient.BanChatMemberAsync(
                    chatId: client.ClientChannelId,
                    userId: client.ClientId
                );

                await botClient.SendTextMessageAsync(
                    chatId: adminGroupId,
                    text: $"Usuario {client.ClientFirstName} (@{client.ClientName}) desactivado y baneado del canal.",
                    messageThreadId: commandsTopicId
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al banear al cliente del canal: {ex.Message}");
                await botClient.SendTextMessageAsync(
                    chatId: adminGroupId,
                    text: $"Usuario desactivado, pero no se pudo banear del canal.",
                    messageThreadId: commandsTopicId
                );
            }
        }

        private async Task HandleReportRequest(Message message)
        {
            // Implementación de la generación de reportes (si es necesario)
            await botClient.SendTextMessageAsync(
                chatId: adminGroupId,
                text: "Función de reporte no implementada aún.",
                messageThreadId: reportsTopicId
            );
        }

        private async Task HandleReminderMessage(Message message)
        {
            try
            {
                var operatorId = message.From.Id;

                // Verificar si el operador está en medio del proceso
                if (operatorReminderStates.ContainsKey(operatorId) && operatorReminderStates[operatorId].WaitingForTime)
                {
                    // Procesar la fecha y hora ingresada
                    var state = operatorReminderStates[operatorId];

                    if (DateTime.TryParseExact(message.Text, "dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime scheduledTime))
                    {
                        state.ScheduledTime = scheduledTime;
                        state.WaitingForTime = false;

                        // Crear el recordatorio
                        var reminder = new Reminder
                        {
                            RemClientId = 0, // 0 indica que es para todos los clientes
                            RemMediaType = state.MediaType,
                            RemMediaFilePath = state.MediaFilePath,
                            RemText = state.ReminderText,
                            RemTime = state.ScheduledTime.Value,
                            RemSended = false
                        };

                        // Guardar el recordatorio en la base de datos
                        Database.AddReminder(reminder);

                        // Programar el envío del recordatorio
                        ScheduleReminderForAllClients(reminder);

                        // Limpiar el estado
                        operatorReminderStates.Remove(operatorId);

                        await botClient.SendTextMessageAsync(
                            chatId: adminGroupId,
                            text: $"✅ Recordatorio programado para el {scheduledTime:dd/MM/yyyy HH:mm}.",
                            messageThreadId: remindersTopicId
                        );
                    }
                    else
                    {
                        await botClient.SendTextMessageAsync(
                            chatId: adminGroupId,
                            text: "Formato de fecha y hora inválido. Por favor, ingresa la fecha y hora en el formato dd/MM/yyyy HH:mm.",
                            messageThreadId: remindersTopicId
                        );
                    }
                }
                else
                {
                    // Iniciar el proceso de programación de un nuevo recordatorio
                    // Verificar que el mensaje tenga texto o un medio
                    if (string.IsNullOrEmpty(message.Text) && message.Type == MessageType.Text)
                    {
                        await botClient.SendTextMessageAsync(
                            chatId: adminGroupId,
                            text: "Por favor, envía un mensaje con texto y/o un medio para programar un recordatorio.",
                            messageThreadId: remindersTopicId
                        );
                        return;
                    }

                    // Detectar el tipo de medio y guardar el archivo
                    string mediaType = "text";
                    string mediaFilePath = null;

                    if (message.Photo != null && message.Photo.Length > 0)
                    {
                        mediaType = "photo";
                        var photo = message.Photo.Last();
                        var file = await botClient.GetFileAsync(photo.FileId);

                        string remindersMediaFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "reminders_media");
                        Directory.CreateDirectory(remindersMediaFolder);

                        mediaFilePath = Path.Combine(remindersMediaFolder, $"reminder_{photo.FileUniqueId}.jpg");
                        using (var saveMediaStream = new FileStream(mediaFilePath, FileMode.Create))
                        {
                            await botClient.DownloadFileAsync(file.FilePath, saveMediaStream);
                        }
                    }
                    else if (message.Video != null)
                    {
                        mediaType = "video";
                        var video = message.Video;
                        var file = await botClient.GetFileAsync(video.FileId);

                        string remindersMediaFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "reminders_media");
                        Directory.CreateDirectory(remindersMediaFolder);

                        mediaFilePath = Path.Combine(remindersMediaFolder, $"reminder_{video.FileUniqueId}.mp4");
                        using (var saveMediaStream = new FileStream(mediaFilePath, FileMode.Create))
                        {
                            await botClient.DownloadFileAsync(file.FilePath, saveMediaStream);
                        }
                    }
                    else if (message.VideoNote != null)
                    {
                        mediaType = "video note";
                        var video = message.VideoNote;
                        var file = await botClient.GetFileAsync(video.FileId);

                        string remindersMediaFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "reminders_media");
                        Directory.CreateDirectory(remindersMediaFolder);

                        mediaFilePath = Path.Combine(remindersMediaFolder, $"reminder_{video.FileUniqueId}.mpeg4");
                        using (var saveMediaStream = new FileStream(mediaFilePath, FileMode.Create))
                        {
                            await botClient.DownloadFileAsync(file.FilePath, saveMediaStream);
                        }
                    }
                    else if (message.Audio != null)
                    {
                        mediaType = "audio";
                        var audio = message.Audio;
                        var file = await botClient.GetFileAsync(audio.FileId);

                        string remindersMediaFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "reminders_media");
                        Directory.CreateDirectory(remindersMediaFolder);

                        mediaFilePath = Path.Combine(remindersMediaFolder, $"reminder_{audio.FileUniqueId}.mp3");
                        using (var saveMediaStream = new FileStream(mediaFilePath, FileMode.Create))
                        {
                            await botClient.DownloadFileAsync(file.FilePath, saveMediaStream);
                        }
                    }
                    else if (message.Voice != null)
                    {
                        mediaType = "voice";
                        var audio = message.Voice;
                        var file = await botClient.GetFileAsync(audio.FileId);

                        string remindersMediaFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "reminders_media");
                        Directory.CreateDirectory(remindersMediaFolder);

                        mediaFilePath = Path.Combine(remindersMediaFolder, $"reminder_{audio.FileUniqueId}.ogg");
                        using (var saveMediaStream = new FileStream(mediaFilePath, FileMode.Create))
                        {
                            await botClient.DownloadFileAsync(file.FilePath, saveMediaStream);
                        }
                    }
                    else if (message.Document != null)
                    {
                        mediaType = "document";
                        var document = message.Document;
                        var file = await botClient.GetFileAsync(document.FileId);

                        string remindersMediaFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "reminders_media");
                        Directory.CreateDirectory(remindersMediaFolder);

                        mediaFilePath = Path.Combine(remindersMediaFolder, $"reminder_{document.FileUniqueId}_{document.FileName}");
                        using (var saveMediaStream = new FileStream(mediaFilePath, FileMode.Create))
                        {
                            await botClient.DownloadFileAsync(file.FilePath, saveMediaStream);
                        }
                    }

                    // Obtener el texto del recordatorio
                    string reminderText = message.Caption ?? message.Text;

                    // Guardar el estado del operador
                    operatorReminderStates[operatorId] = new ReminderState
                    {
                        ReminderText = reminderText,
                        MediaType = mediaType,
                        MediaFilePath = mediaFilePath,
                        WaitingForTime = true
                    };

                    // Solicitar la fecha y hora al operador
                    await botClient.SendTextMessageAsync(
                        chatId: adminGroupId,
                        text: "Por favor, ingresa la fecha y hora para el recordatorio en formato dd/MM/yyyy HH:mm.",
                        messageThreadId: remindersTopicId
                    );
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en HandleReminderMessage: {ex.Message}");
                await SendErrorToGeneral($"Error en HandleReminderMessage: {ex.Message}");
            }
        }

        private void ScheduleReminderForAllClients(Reminder reminder)
        {
            var delay = (reminder.RemTime - DateTime.Now).TotalMilliseconds;
            if (delay <= 0)
            {
                delay = 0;
            }

            var timer = new System.Timers.Timer(delay);
            timer.Elapsed += async (sender, e) =>
            {
                await SendReminderToAllClients(reminder);
                timer.Stop();
                timer.Dispose();
            };
            timer.AutoReset = false;
            timer.Start();
        }

        private async Task SendReminderToAllClients(Reminder reminder)
        {
            try
            {
                // Obtener todos los clientes activos
                var activeClients = Database.GetAllActiveClients();

                if (reminder.RemText == null) reminder.RemText = "";
                // Enviar mensajes en lotes para evitar sobrecargar el bot
                int batchSize = 30; // Puedes ajustar este valor según tus necesidades
                for (int i = 0; i < activeClients.Count; i += batchSize)
                {
                    var batchClients = activeClients.Skip(i).Take(batchSize);
                    var tasks = new List<Task>();

                    foreach (var client in batchClients)
                    {
                        tasks.Add(Task.Run(async () =>
                        {
                            try
                            {
                                await SendMediaToClient(client.ClientId, reminder);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Error al enviar recordatorio al cliente {client.ClientId}: {ex.Message}");
                                await SendErrorToGeneral($"Error al enviar recordatorio al cliente {client.ClientId}: {ex.Message}");
                            }
                        }));
                    }

                    // Esperar a que se completen todas las tareas del lote
                    await Task.WhenAll(tasks);

                    // Esperar un tiempo antes de enviar el siguiente lote para evitar superar los límites
                    await Task.Delay(1000); // Esperar 1 segundo, puedes ajustar este valor
                }

                // Marcar el recordatorio como enviado
                reminder.RemSended = true;
                Database.UpdateReminder(reminder);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al enviar recordatorio a todos los clientes: {ex.Message}");
                await SendErrorToGeneral($"Error al enviar recordatorio a todos los clientes: {ex.Message}");
            }
        }

        private void ScheduleReminders(Client client)
        {
            // Programar recordatorios a los 30 minutos, 1 hora y 2 horas
            // Para cada uno, crear un recordatorio y almacenarlo en la base de datos

            var reminderTimes = new List<TimeSpan>
            {
                TimeSpan.FromMinutes(30),
                TimeSpan.FromHours(1),
                TimeSpan.FromHours(2)
            };

            foreach (var time in reminderTimes)
            {
                var reminderType = time == TimeSpan.FromMinutes(30) ? "30min" :
                                   time == TimeSpan.FromHours(1) ? "1hour" : "2hours";

                var reminder = new Reminder
                {
                    RemClientId = client.ClientId,
                    RemMediaType = "photo",
                    RemMediaFilePath = reminderImages[reminderType],
                    RemText = reminderTexts[reminderType],
                    RemTime = DateTime.Now.Add(time),
                    RemSended = false
                };
                Database.AddReminder(reminder);

                // Programar el recordatorio
                var delay = (reminder.RemTime - DateTime.Now).TotalMilliseconds;
                if (delay > 0)
                {
                    var timer = new System.Timers.Timer(delay);
                    timer.Elapsed += async (sender, e) =>
                    {
                        await SendReminder(reminder);
                        timer.Stop();
                        timer.Dispose();
                    };
                    timer.AutoReset = false;
                    timer.Start();
                }
            }
        }

        private async Task SendReminder(Reminder reminder)
        {
            try
            {
                // Obtener el cliente
                var client = Database.GetClientById(reminder.RemClientId);
                if (client == null || !client.IsActive)
                {
                    return;
                }

                await SendMediaToClient(client.ClientId, reminder);

                // Marcar recordatorio como enviado
                reminder.RemSended = true;
                Database.UpdateReminder(reminder);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al enviar recordatorio: {ex.Message}");
                await SendErrorToGeneral($"Error al enviar recordatorio: {ex.Message}");
            }
        }

        private async Task SendMediaToClient(long clientId, Reminder reminder)
        {
            // Obtener el operador asignado
            var client = Database.GetClientById(clientId);
            var operatorAssigned = Database.GetOperatorById(client.ClientOperatorId);
            if (operatorAssigned == null)
            {
                return;
            }

            // Crear el botón con el enlace al perfil del operador
            var operatorProfileUrl = $"https://t.me/{operatorAssigned.OpUsername}";
            var inlineKeyboard = new InlineKeyboardMarkup(new[]
            {
                InlineKeyboardButton.WithUrl($"{client.ClientFirstName} escribame y empiece a ganar!", operatorProfileUrl)
            });

            // Enviar el recordatorio al cliente con el botón
            switch (reminder.RemMediaType)
            {
                case "photo":
                    if (System.IO.File.Exists(reminder.RemMediaFilePath))
                    {
                        using (var fileStream = new FileStream(reminder.RemMediaFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                        {
                            var inputFile = InputFile.FromStream(fileStream);

                            await botClient.SendPhotoAsync(
                                chatId: clientId,
                                photo: inputFile,
                                caption: reminder.RemText,
                                replyMarkup: inlineKeyboard
                            );
                        }
                    }
                    break;

                case "video":
                    if (System.IO.File.Exists(reminder.RemMediaFilePath))
                    {
                        using (var fileStream = new FileStream(reminder.RemMediaFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                        {
                            var inputFile = InputFile.FromStream(fileStream);

                            await botClient.SendVideoAsync(
                                chatId: clientId,
                                video: inputFile,
                                replyMarkup: inlineKeyboard
                            );
                        }
                    }
                    break;
                case "video note":
                    if (System.IO.File.Exists(reminder.RemMediaFilePath))
                    {
                        using (var fileStream = new FileStream(reminder.RemMediaFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                        {
                            var inputFile = InputFile.FromStream(fileStream);

                            await botClient.SendVideoNoteAsync(
                                chatId: clientId,
                                videoNote: inputFile,
                                replyMarkup: inlineKeyboard
                            );
                        }
                    }
                    break;

                case "audio":
                    if (System.IO.File.Exists(reminder.RemMediaFilePath))
                    {
                        using (var fileStream = new FileStream(reminder.RemMediaFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                        {
                            var inputFile = InputFile.FromStream(fileStream);

                            await botClient.SendAudioAsync(
                                chatId: clientId,
                                audio: inputFile,
                                replyMarkup: inlineKeyboard
                            );
                        }
                    }
                    break;
                case "voice":
                    if (System.IO.File.Exists(reminder.RemMediaFilePath))
                    {
                        using (var fileStream = new FileStream(reminder.RemMediaFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                        {
                            var inputFile = InputFile.FromStream(fileStream);

                            await botClient.SendVoiceAsync(
                                chatId: clientId,
                                voice: inputFile,
                                replyMarkup: inlineKeyboard
                            );
                        }
                    }
                    break;

                case "document":
                    if (System.IO.File.Exists(reminder.RemMediaFilePath))
                    {
                        using (var fileStream = new FileStream(reminder.RemMediaFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                        {
                            var inputFile = InputFile.FromStream(fileStream);

                            await botClient.SendDocumentAsync(
                                chatId: clientId,
                                document: inputFile,
                                caption: reminder.RemText,
                                replyMarkup: inlineKeyboard
                            );
                        }
                    }
                    break;

                case "text":
                default:
                    await botClient.SendTextMessageAsync(
                        chatId: clientId,
                        text: reminder.RemText,
                        replyMarkup: inlineKeyboard
                    );
                    break;
            }
        }

        private async Task SendErrorToGeneral(string errorMessage)
        {
            try
            {
                await botClient.SendTextMessageAsync(
                    chatId: adminGroupId,
                    text: $"⚠️ Error: {errorMessage}",
                    messageThreadId: generalTopicId
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al enviar mensaje de error al tema General: {ex.Message}");
            }
        }

        private async Task SendMessageToGeneral(string message)
        {
            try
            {
                await botClient.SendTextMessageAsync(
                    chatId: adminGroupId,
                    text: message
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al enviar mensaje al tema General: {ex.Message}");
            }
        }
    }
}
