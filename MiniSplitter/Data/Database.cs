using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using MiniSplitter.Models;

namespace MiniSplitter.Data
{
    public static class Database
    {
        private static string connectionString;
        private static readonly object dbLock = new object();

        public static void Initialize()
        {
            var dbFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "database.sqlite");
            connectionString = $"Data Source={dbFilePath};Version=3;";

            if (!File.Exists(dbFilePath))
            {
                SQLiteConnection.CreateFile(dbFilePath);
            }

            CreateTables();
        }

        private static void CreateTables()
        {
            lock (dbLock)
            {
                using var connection = new SQLiteConnection(connectionString);
                connection.Open();

                // Crear tabla de Clients
                var createClientsTable = @"
                    CREATE TABLE IF NOT EXISTS Clients (
                        client_id INTEGER PRIMARY KEY,
                        client_name TEXT,
                        client_first_name TEXT,
                        client_channel_id INTEGER,
                        client_operator_id INTEGER,
                        is_active BOOLEAN,
                        client_wrote BOOLEAN,
                        client_thread_id INTEGER,
                        client_entry_date DATETIME
                    );
                ";

                // Crear tabla de Operators
                var createOperatorsTable = @"
                    CREATE TABLE IF NOT EXISTS Operators (
                        op_id INTEGER PRIMARY KEY,
                        op_username TEXT,
                        op_channel INTEGER,
                        is_active BOOLEAN,
                        assigned_clients_today INTEGER
                    );
                ";

                // Crear tabla de Channels
                var createChannelsTable = @"
                    CREATE TABLE IF NOT EXISTS Channels (
                        id INTEGER PRIMARY KEY AUTOINCREMENT,
                        chan_id INTEGER,
                        chan_name TEXT,
                        is_active BOOLEAN
                    );
                ";

                // Crear tabla de Reminders
                var createRemindersTable = @"
                    CREATE TABLE IF NOT EXISTS Reminders (
                        rem_id INTEGER PRIMARY KEY AUTOINCREMENT,
                        rem_client_id INTEGER,
                        rem_media_type TEXT,
                        rem_media_file_path TEXT,
                        rem_text TEXT,
                        rem_time DATETIME,
                        rem_sended BOOLEAN
                    );
                ";

                // Crear tabla de Reports
                var createReportsTable = @"
                    CREATE TABLE IF NOT EXISTS Reports (
                        report_id INTEGER PRIMARY KEY AUTOINCREMENT,
                        report_date DATE,
                        total_clients INTEGER,
                        total_operators INTEGER
                    );
                ";

                using var command = new SQLiteCommand(connection);
                command.CommandText = createClientsTable;
                command.ExecuteNonQuery();

                command.CommandText = createOperatorsTable;
                command.ExecuteNonQuery();

                command.CommandText = createChannelsTable;
                command.ExecuteNonQuery();

                command.CommandText = createRemindersTable;
                command.ExecuteNonQuery();

                command.CommandText = createReportsTable;
                command.ExecuteNonQuery();
            }
        }

        // Métodos para manejar Clients
        public static void AddClient(Client client)
        {
            lock (dbLock)
            {
                using var connection = new SQLiteConnection(connectionString);
                connection.Open();

                var query = @"
                    INSERT INTO Clients (client_id, client_name, client_first_name, client_channel_id, client_operator_id, is_active, client_wrote, client_thread_id, client_entry_date)
                    VALUES (@ClientId, @ClientName, @ClientFirstName, @ClientChannelId, @ClientOperatorId, @IsActive, @ClientWrote, @ClientThreadId, @ClientEntryDate);
                ";

                using var command = new SQLiteCommand(query, connection);
                command.Parameters.AddWithValue("@ClientId", client.ClientId);
                command.Parameters.AddWithValue("@ClientName", client.ClientName ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@ClientFirstName", client.ClientFirstName ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@ClientChannelId", client.ClientChannelId);
                command.Parameters.AddWithValue("@ClientOperatorId", client.ClientOperatorId);
                command.Parameters.AddWithValue("@IsActive", client.IsActive ? 1 : 0);
                command.Parameters.AddWithValue("@ClientWrote", client.ClientWrote ? 1 : 0);
                command.Parameters.AddWithValue("@ClientThreadId", client.ClientThreadId);
                command.Parameters.AddWithValue("@ClientEntryDate", client.ClientEntryDate);

                command.ExecuteNonQuery();
            }
        }

        public static void UpdateClient(Client client)
        {
            lock (dbLock)
            {
                using var connection = new SQLiteConnection(connectionString);
                connection.Open();

                var query = @"
                    UPDATE Clients
                    SET client_name = @ClientName,
                        client_first_name = @ClientFirstName,
                        client_channel_id = @ClientChannelId,
                        client_operator_id = @ClientOperatorId,
                        is_active = @IsActive,
                        client_wrote = @ClientWrote,
                        client_thread_id = @ClientThreadId,
                        client_entry_date = @ClientEntryDate
                    WHERE client_id = @ClientId;
                ";

                using var command = new SQLiteCommand(query, connection);
                command.Parameters.AddWithValue("@ClientId", client.ClientId);
                command.Parameters.AddWithValue("@ClientName", client.ClientName ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@ClientFirstName", client.ClientFirstName ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@ClientChannelId", client.ClientChannelId);
                command.Parameters.AddWithValue("@ClientOperatorId", client.ClientOperatorId);
                command.Parameters.AddWithValue("@IsActive", client.IsActive ? 1 : 0);
                command.Parameters.AddWithValue("@ClientWrote", client.ClientWrote ? 1 : 0);
                command.Parameters.AddWithValue("@ClientThreadId", client.ClientThreadId);
                command.Parameters.AddWithValue("@ClientEntryDate", client.ClientEntryDate);

                command.ExecuteNonQuery();
            }
        }

        public static Client GetClientById(long clientId)
        {
            lock (dbLock)
            {
                using var connection = new SQLiteConnection(connectionString);
                connection.Open();

                var query = "SELECT * FROM Clients WHERE client_id = @ClientId;";
                using var command = new SQLiteCommand(query, connection);
                command.Parameters.AddWithValue("@ClientId", clientId);

                using var reader = command.ExecuteReader();
                if (reader.Read())
                {
                    return new Client
                    {
                        ClientId = reader.GetInt64(reader.GetOrdinal("client_id")),
                        ClientName = reader["client_name"] as string,
                        ClientFirstName = reader["client_first_name"] as string,
                        ClientChannelId = reader.GetInt64(reader.GetOrdinal("client_channel_id")),
                        ClientOperatorId = reader.GetInt64(reader.GetOrdinal("client_operator_id")),
                        IsActive = reader.GetBoolean(reader.GetOrdinal("is_active")),
                        ClientWrote = reader.GetBoolean(reader.GetOrdinal("client_wrote")),
                        ClientThreadId = reader.GetInt32(reader.GetOrdinal("client_thread_id")),
                        ClientEntryDate = reader.GetDateTime(reader.GetOrdinal("client_entry_date"))
                    };
                }
                return null;
            }
        }

        public static Client GetClientByThreadId(int threadId)
        {
            lock (dbLock)
            {
                using var connection = new SQLiteConnection(connectionString);
                connection.Open();

                var query = "SELECT * FROM Clients WHERE client_thread_id = @ThreadId;";
                using var command = new SQLiteCommand(query, connection);
                command.Parameters.AddWithValue("@ThreadId", threadId);

                using var reader = command.ExecuteReader();
                if (reader.Read())
                {
                    return new Client
                    {
                        ClientId = reader.GetInt64(reader.GetOrdinal("client_id")),
                        ClientName = reader["client_name"] as string,
                        ClientFirstName = reader["client_first_name"] as string,
                        ClientChannelId = reader.GetInt64(reader.GetOrdinal("client_channel_id")),
                        ClientOperatorId = reader.GetInt64(reader.GetOrdinal("client_operator_id")),
                        IsActive = reader.GetBoolean(reader.GetOrdinal("is_active")),
                        ClientWrote = reader.GetBoolean(reader.GetOrdinal("client_wrote")),
                        ClientThreadId = reader.GetInt32(reader.GetOrdinal("client_thread_id")),
                        ClientEntryDate = reader.GetDateTime(reader.GetOrdinal("client_entry_date"))
                    };
                }
                return null;
            }
        }

        public static List<Client> GetAllActiveClients()
        {
            var clients = new List<Client>();
            lock (dbLock)
            {
                using var connection = new SQLiteConnection(connectionString);
                connection.Open();

                var query = "SELECT * FROM Clients WHERE is_active = 1;";
                using var command = new SQLiteCommand(query, connection);

                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    var client = new Client
                    {
                        ClientId = reader.GetInt64(reader.GetOrdinal("client_id")),
                        ClientName = reader["client_name"] as string,
                        ClientFirstName = reader["client_first_name"] as string,
                        ClientChannelId = reader.GetInt64(reader.GetOrdinal("client_channel_id")),
                        ClientOperatorId = reader.GetInt64(reader.GetOrdinal("client_operator_id")),
                        IsActive = reader.GetBoolean(reader.GetOrdinal("is_active")),
                        ClientWrote = reader.GetBoolean(reader.GetOrdinal("client_wrote")),
                        ClientThreadId = reader.GetInt32(reader.GetOrdinal("client_thread_id")),
                        ClientEntryDate = reader.GetDateTime(reader.GetOrdinal("client_entry_date"))
                    };
                    clients.Add(client);
                }
            }
            return clients;
        }

        // Métodos para manejar Operators
        public static void AddOperator(Operator op)
        {
            lock (dbLock)
            {
                using var connection = new SQLiteConnection(connectionString);
                connection.Open();

                var query = @"
                    INSERT INTO Operators (op_id, op_username, op_channel, is_active, assigned_clients_today)
                    VALUES (@OpId, @OpUsername, @OpChannel, @IsActive, @AssignedClientsToday);
                ";

                using var command = new SQLiteCommand(query, connection);
                command.Parameters.AddWithValue("@OpId", op.OpId);
                command.Parameters.AddWithValue("@OpUsername", op.OpUsername);
                command.Parameters.AddWithValue("@OpChannel", op.OpChannel);
                command.Parameters.AddWithValue("@IsActive", op.IsActive ? 1 : 0);
                command.Parameters.AddWithValue("@AssignedClientsToday", op.AssignedClientsToday);

                command.ExecuteNonQuery();
            }
        }

        public static void UpdateOperator(Operator op)
        {
            lock (dbLock)
            {
                using var connection = new SQLiteConnection(connectionString);
                connection.Open();

                var query = @"
                    UPDATE Operators
                    SET op_username = @OpUsername,
                        op_channel = @OpChannel,
                        is_active = @IsActive,
                        assigned_clients_today = @AssignedClientsToday
                    WHERE op_id = @OpId;
                ";

                using var command = new SQLiteCommand(query, connection);
                command.Parameters.AddWithValue("@OpId", op.OpId);
                command.Parameters.AddWithValue("@OpUsername", op.OpUsername);
                command.Parameters.AddWithValue("@OpChannel", op.OpChannel);
                command.Parameters.AddWithValue("@IsActive", op.IsActive ? 1 : 0);
                command.Parameters.AddWithValue("@AssignedClientsToday", op.AssignedClientsToday);

                command.ExecuteNonQuery();
            }
        }

        public static Operator GetOperatorById(long opId)
        {
            lock (dbLock)
            {
                using var connection = new SQLiteConnection(connectionString);
                connection.Open();

                var query = "SELECT * FROM Operators WHERE op_id = @OpId;";
                using var command = new SQLiteCommand(query, connection);
                command.Parameters.AddWithValue("@OpId", opId);

                using var reader = command.ExecuteReader();
                if (reader.Read())
                {
                    return new Operator
                    {
                        OpId = reader.GetInt64(reader.GetOrdinal("op_id")),
                        OpUsername = reader["op_username"] as string,
                        OpChannel = reader.GetInt64(reader.GetOrdinal("op_channel")),
                        IsActive = reader.GetBoolean(reader.GetOrdinal("is_active")),
                        AssignedClientsToday = reader.GetInt32(reader.GetOrdinal("assigned_clients_today"))
                    };
                }
                return null;
            }
        }

        public static List<Operator> GetAllOperators()
        {
            var operators = new List<Operator>();
            lock (dbLock)
            {
                using var connection = new SQLiteConnection(connectionString);
                connection.Open();

                var query = "SELECT * FROM Operators;";
                using var command = new SQLiteCommand(query, connection);

                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    var op = new Operator
                    {
                        OpId = reader.GetInt64(reader.GetOrdinal("op_id")),
                        OpUsername = reader["op_username"] as string,
                        OpChannel = reader.GetInt64(reader.GetOrdinal("op_channel")),
                        IsActive = reader.GetBoolean(reader.GetOrdinal("is_active")),
                        AssignedClientsToday = reader.GetInt32(reader.GetOrdinal("assigned_clients_today"))
                    };
                    operators.Add(op);
                }
            }
            return operators;
        }

        public static Operator GetOperatorWithLeastClients(long channelId)
        {
            lock (dbLock)
            {
                using var connection = new SQLiteConnection(connectionString);
                connection.Open();

                var query = @"
                    SELECT * FROM Operators
                    WHERE op_channel = @ChannelId AND is_active = 1
                    ORDER BY assigned_clients_today ASC
                    LIMIT 1;
                ";

                using var command = new SQLiteCommand(query, connection);
                command.Parameters.AddWithValue("@ChannelId", channelId);

                using var reader = command.ExecuteReader();
                if (reader.Read())
                {
                    return new Operator
                    {
                        OpId = reader.GetInt64(reader.GetOrdinal("op_id")),
                        OpUsername = reader["op_username"] as string,
                        OpChannel = reader.GetInt64(reader.GetOrdinal("op_channel")),
                        IsActive = reader.GetBoolean(reader.GetOrdinal("is_active")),
                        AssignedClientsToday = reader.GetInt32(reader.GetOrdinal("assigned_clients_today"))
                    };
                }
                return null;
            }
        }

        // Métodos para manejar Channels
        public static void AddChannel(Channel channel)
        {
            lock (dbLock)
            {
                using var connection = new SQLiteConnection(connectionString);
                connection.Open();

                var query = @"
                    INSERT INTO Channels (chan_id, chan_name, is_active)
                    VALUES (@ChanId, @ChanName, @IsActive);
                ";

                using var command = new SQLiteCommand(query, connection);
                command.Parameters.AddWithValue("@ChanId", channel.ChanId);
                command.Parameters.AddWithValue("@ChanName", channel.ChanName);
                command.Parameters.AddWithValue("@IsActive", channel.IsActive ? 1 : 0);

                command.ExecuteNonQuery();
            }
        }

        public static void UpdateChannel(Channel channel)
        {
            lock (dbLock)
            {
                using var connection = new SQLiteConnection(connectionString);
                connection.Open();

                var query = @"
                    UPDATE Channels
                    SET chan_id = @ChanId,
                        chan_name = @ChanName,
                        is_active = @IsActive
                    WHERE id = @Id;
                ";

                using var command = new SQLiteCommand(query, connection);
                command.Parameters.AddWithValue("@Id", channel.Id);
                command.Parameters.AddWithValue("@ChanId", channel.ChanId);
                command.Parameters.AddWithValue("@ChanName", channel.ChanName);
                command.Parameters.AddWithValue("@IsActive", channel.IsActive ? 1 : 0);

                command.ExecuteNonQuery();
            }
        }

        public static Channel GetChannelById(int id)
        {
            lock (dbLock)
            {
                using var connection = new SQLiteConnection(connectionString);
                connection.Open();

                var query = "SELECT * FROM Channels WHERE id = @Id;";
                using var command = new SQLiteCommand(query, connection);
                command.Parameters.AddWithValue("@Id", id);

                using var reader = command.ExecuteReader();
                if (reader.Read())
                {
                    return new Channel
                    {
                        Id = reader.GetInt32(reader.GetOrdinal("id")),
                        ChanId = reader.GetInt64(reader.GetOrdinal("chan_id")),
                        ChanName = reader["chan_name"] as string,
                        IsActive = reader.GetBoolean(reader.GetOrdinal("is_active"))
                    };
                }
                return null;
            }
        }

        public static Channel GetChannelByTelegramId(long chanId)
        {
            lock (dbLock)
            {
                using var connection = new SQLiteConnection(connectionString);
                connection.Open();

                var query = "SELECT * FROM Channels WHERE chan_id = @ChanId;";
                using var command = new SQLiteCommand(query, connection);
                command.Parameters.AddWithValue("@ChanId", chanId);

                using var reader = command.ExecuteReader();
                if (reader.Read())
                {
                    return new Channel
                    {
                        Id = reader.GetInt32(reader.GetOrdinal("id")),
                        ChanId = reader.GetInt64(reader.GetOrdinal("chan_id")),
                        ChanName = reader["chan_name"] as string,
                        IsActive = reader.GetBoolean(reader.GetOrdinal("is_active"))
                    };
                }
                return null;
            }
        }

        public static List<Channel> GetAllChannels()
        {
            var channels = new List<Channel>();
            lock (dbLock)
            {
                using var connection = new SQLiteConnection(connectionString);
                connection.Open();

                var query = "SELECT * FROM Channels;";
                using var command = new SQLiteCommand(query, connection);

                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    var channel = new Channel
                    {
                        Id = reader.GetInt32(reader.GetOrdinal("id")),
                        ChanId = reader.GetInt64(reader.GetOrdinal("chan_id")),
                        ChanName = reader["chan_name"] as string,
                        IsActive = reader.GetBoolean(reader.GetOrdinal("is_active"))
                    };
                    channels.Add(channel);
                }
            }
            return channels;
        }

        // Métodos para manejar Reminders
        public static void AddReminder(Reminder reminder)
        {
            lock (dbLock)
            {
                using var connection = new SQLiteConnection(connectionString);
                connection.Open();

                var query = @"
                    INSERT INTO Reminders (rem_client_id, rem_media_type, rem_media_file_path, rem_text, rem_time, rem_sended)
                    VALUES (@RemClientId, @RemMediaType, @RemMediaFilePath, @RemText, @RemTime, @RemSended);
                ";

                using var command = new SQLiteCommand(query, connection);
                command.Parameters.AddWithValue("@RemClientId", reminder.RemClientId);
                command.Parameters.AddWithValue("@RemMediaType", reminder.RemMediaType ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@RemMediaFilePath", reminder.RemMediaFilePath ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@RemText", reminder.RemText ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@RemTime", reminder.RemTime);
                command.Parameters.AddWithValue("@RemSended", reminder.RemSended ? 1 : 0);

                command.ExecuteNonQuery();
            }
        }

        public static void UpdateReminder(Reminder reminder)
        {
            lock (dbLock)
            {
                using var connection = new SQLiteConnection(connectionString);
                connection.Open();

                var query = @"
                    UPDATE Reminders
                    SET rem_sended = @RemSended
                    WHERE rem_id = @RemId;
                ";

                using var command = new SQLiteCommand(query, connection);
                command.Parameters.AddWithValue("@RemId", reminder.RemId);
                command.Parameters.AddWithValue("@RemSended", reminder.RemSended ? 1 : 0);

                command.ExecuteNonQuery();
            }
        }

        public static List<Reminder> GetPendingReminders()
        {
            var reminders = new List<Reminder>();
            lock (dbLock)
            {
                using var connection = new SQLiteConnection(connectionString);
                connection.Open();

                var query = "SELECT * FROM Reminders WHERE rem_sended = 0 AND rem_time <= @CurrentTime;";
                using var command = new SQLiteCommand(query, connection);
                command.Parameters.AddWithValue("@CurrentTime", DateTime.Now);

                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    var reminder = new Reminder
                    {
                        RemId = reader.GetInt32(reader.GetOrdinal("rem_id")),
                        RemClientId = reader.GetInt64(reader.GetOrdinal("rem_client_id")),
                        RemMediaType = reader["rem_media_type"] as string,
                        RemMediaFilePath = reader["rem_media_file_path"] as string,
                        RemText = reader["rem_text"] as string,
                        RemTime = reader.GetDateTime(reader.GetOrdinal("rem_time")),
                        RemSended = reader.GetBoolean(reader.GetOrdinal("rem_sended"))
                    };
                    reminders.Add(reminder);
                }
            }
            return reminders;
        }

        // Métodos para manejar Reports
        public static void AddReport(Report report)
        {
            lock (dbLock)
            {
                using var connection = new SQLiteConnection(connectionString);
                connection.Open();

                var query = @"
                    INSERT INTO Reports (report_date, total_clients, total_operators)
                    VALUES (@ReportDate, @TotalClients, @TotalOperators);
                ";

                using var command = new SQLiteCommand(query, connection);
                command.Parameters.AddWithValue("@ReportDate", report.ReportDate);
                command.Parameters.AddWithValue("@TotalClients", report.TotalClients);
                command.Parameters.AddWithValue("@TotalOperators", report.TotalOperators);

                command.ExecuteNonQuery();
            }
        }

        public static Report GetReportByDate(DateTime date)
        {
            lock (dbLock)
            {
                using var connection = new SQLiteConnection(connectionString);
                connection.Open();

                var query = "SELECT * FROM Reports WHERE report_date = @ReportDate;";
                using var command = new SQLiteCommand(query, connection);
                command.Parameters.AddWithValue("@ReportDate", date.Date);

                using var reader = command.ExecuteReader();
                if (reader.Read())
                {
                    return new Report
                    {
                        ReportId = reader.GetInt32(reader.GetOrdinal("report_id")),
                        ReportDate = reader.GetDateTime(reader.GetOrdinal("report_date")),
                        TotalClients = reader.GetInt32(reader.GetOrdinal("total_clients")),
                        TotalOperators = reader.GetInt32(reader.GetOrdinal("total_operators"))
                    };
                }
                return null;
            }
        }

        // Métodos adicionales según sea necesario...
    }
}
