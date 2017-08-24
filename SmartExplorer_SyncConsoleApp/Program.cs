using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace SmartExplorer_SyncConsoleApp
{
    class Program
    {
        // Wallet
        static string serverURL = "http://127.0.0.1:9679/";
        static string serverUser = "user";
        static string serverPass = "pass";

        // SQL
        static string connString = @"Data Source=.\SQL2016;Initial Catalog = SmartExplorer;Integrated Security=True;";

        static void Main(string[] args)
        {

            //Body
            string startBlockHash = GetStartBlockHash();
            Block curBlock = new Block();
            if (startBlockHash == "") // Start from scratch
            {
                startBlockHash = "00000009c4e61bee0e8d6236f847bb1dd23f4c61ca5240b74852184c9bf98c30"; // Block 1
                curBlock = GetBlock(startBlockHash);
                InsertBlock(curBlock);
                foreach (string txid in curBlock.tx)
                {
                    Transaction nextTransaction = GetTransaction(txid);
                    InsertTransaction(nextTransaction);

                    var countInput = 0;
                    // Input
                    foreach (TransactionInput transactionInput in nextTransaction.vin)
                    {
                        InsertTransactionInput(transactionInput, txid, countInput);
                        countInput++;
                    }

                    // Output
                    foreach (TransactionOutput transactionOutput in nextTransaction.vout)
                    {
                        if (transactionOutput.scriptPubKey.addresses != null)
                        {
                            if (transactionOutput.scriptPubKey.addresses.Length > 1)
                            {
                                Debug.WriteLine("2> Address: " + txid);
                            }
                            InsertTransactionOutput(transactionOutput, txid);
                        }
                        else
                        {
                            Debug.WriteLine("No Address: " + txid);
                        }
                    }

                }
            }
            else
            {
                curBlock = GetBlock(startBlockHash);
            }

            HashSet<string> tx = GetAllTransactions(); // All transactions, needed for ZeroCoin Mint (SmartCash Renew) transaction fix

            while (curBlock.nextblockhash != null)
            {
                // Block
                curBlock = GetBlock(curBlock.nextblockhash);
                InsertBlock(curBlock);

                // Transaction
                foreach (string txid in curBlock.tx)
                {

                    Transaction nextTransaction = GetTransaction(txid);
                    if (nextTransaction.blockhash != curBlock.hash)
                    {
                        continue;
                    }

                    InsertTransaction(nextTransaction);

                    if (!tx.Contains(txid)) //Exclude transactions linked to multiple blocks (Zerocoin Mint/Smartcash Renew)
                    {
                        var countInput = 0;
                        // Input
                        foreach (TransactionInput transactionInput in nextTransaction.vin)
                        {
                            InsertTransactionInput(transactionInput, txid, countInput);
                            countInput++;
                        }

                        // Output
                        foreach (TransactionOutput transactionOutput in nextTransaction.vout)
                        {
                            if (transactionOutput.scriptPubKey.addresses != null)
                            {
                                if (transactionOutput.scriptPubKey.addresses.Length > 1)
                                {
                                    Debug.WriteLine("2> Address: " + txid);
                                }
                                InsertTransactionOutput(transactionOutput, txid);
                            }
                            else
                            {
                                Debug.WriteLine("No Address: " + txid);
                            }
                        }

                        tx.Add(txid);
                    }
                }

                Console.WriteLine(curBlock.height);
            }

            Console.WriteLine("Done");
            Console.ReadLine();
        }

        static string GetStartBlockHash()
        {
            string startBlockHash = "";


            string selectString = "SELECT TOP 1 [Hash] FROM [Block] ORDER BY [Height] DESC";
            using (SqlConnection conn = new SqlConnection(connString))
            {

                using (SqlCommand comm = new SqlCommand(selectString, conn))
                {
                    try
                    {
                        conn.Open();
                        startBlockHash = (string)comm.ExecuteScalar();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("SQL Error" + ex.Message + " : " + "GetStartBlockHash");
                        throw;
                    }
                }
            }

            return startBlockHash;
        }

        static HashSet<string> GetAllTransactions()
        {
            HashSet<string> tx = new HashSet<string>();

            string selectString = "SELECT [Txid] FROM [Transaction]";
            using (SqlConnection conn = new SqlConnection(connString))
            {

                using (SqlCommand comm = new SqlCommand(selectString, conn))
                {
                    try
                    {
                        conn.Open();
                        using (SqlDataReader dr = comm.ExecuteReader())
                        {
                            while (dr.Read())
                            {
                                tx.Add(dr["Txid"].ToString());
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("SQL Error" + ex.Message + " : " + "GetAllTransactions");
                        throw;
                    }
                }
            }

            return tx;
        }

        static Block GetBlock(string blockHash)
        {

            //Authorization Header
            ASCIIEncoding encoding = new ASCIIEncoding();
            byte[] bytesAuthValue = encoding.GetBytes(serverUser + ":" + serverPass);
            string base64AuthValue = Convert.ToBase64String(bytesAuthValue);
            string basicAuthValue = "Basic " + base64AuthValue;

            //Body
            RequestBody requestBody = new RequestBody()
            {
                method = "getblock",
                @params = new object[] { blockHash }
            };
            var jsonRequestBody = new JavaScriptSerializer().Serialize(requestBody);
            byte[] bytesRequestBody = encoding.GetBytes(jsonRequestBody);

            //Post
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(serverURL);
            req.Method = "POST";
            req.ContentType = "application/json";
            req.Headers.Add("Authorization", basicAuthValue);

            Stream newStream = req.GetRequestStream();
            newStream.Write(bytesRequestBody, 0, bytesRequestBody.Length);
            newStream.Close();

            //Response
            HttpWebResponse response = (HttpWebResponse)req.GetResponse();
            if (response.StatusCode == HttpStatusCode.OK)
            {
                using (Stream stream = response.GetResponseStream())
                {
                    StreamReader reader = new StreamReader(stream, Encoding.UTF8);
                    string responseString = reader.ReadToEnd();
                    var responseBlock = new JavaScriptSerializer().Deserialize<ResponseBlock>(responseString);
                    if (responseBlock.error == null || responseBlock.error == "")
                    {
                        return responseBlock.result;
                    }
                    else
                    {
                        Console.WriteLine("Response Error" + responseBlock.error + " : " + jsonRequestBody);
                        throw new Exception();
                    }
                }
            }
            else
            {
                Console.WriteLine("HTTP Error" + response.StatusCode.ToString() + " : " + jsonRequestBody);
                throw new Exception();
            }


        }

        static bool InsertBlock(Block block)
        {
            string cmdString = "INSERT INTO [Block] ([Hash],[Height],[Confirmation],[Size],[Difficulty],[Version],[Time]) VALUES (@Hash, @Height, @Confirmation, @Size, @Difficulty, @Version, @Time)";
            using (SqlConnection conn = new SqlConnection(connString))
            {

                using (SqlCommand comm = new SqlCommand())
                {
                    comm.Connection = conn;
                    comm.CommandText = cmdString;
                    comm.Parameters.AddWithValue("@Hash", block.hash);
                    comm.Parameters.AddWithValue("@Height", block.height);
                    comm.Parameters.AddWithValue("@Confirmation", block.confirmations);
                    comm.Parameters.AddWithValue("@Size", block.size);
                    comm.Parameters.AddWithValue("@Difficulty", block.difficulty);
                    comm.Parameters.AddWithValue("@Version", block.version);
                    comm.Parameters.AddWithValue("@Time", UnixTimeStampToDateTime(block.time));
                    try
                    {
                        conn.Open();
                        comm.ExecuteNonQuery();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("SQL Error" + ex.Message + " : " + new JavaScriptSerializer().Serialize(block));
                        throw;
                    }
                }
            }

            return true;
        }

        static Transaction GetTransaction(string txid)
        {

            //Authorization Header
            ASCIIEncoding encoding = new ASCIIEncoding();
            byte[] bytesAuthValue = encoding.GetBytes(serverUser + ":" + serverPass);
            string base64AuthValue = Convert.ToBase64String(bytesAuthValue);
            string basicAuthValue = "Basic " + base64AuthValue;

            //Body
            RequestBody requestBody = new RequestBody()
            {
                method = "getrawtransaction",
                @params = new object[] { txid, 1 }
            };
            var jsonRequestBody = new JavaScriptSerializer().Serialize(requestBody);
            byte[] bytesRequestBody = encoding.GetBytes(jsonRequestBody);

            //Post
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(serverURL);
            req.Method = "POST";
            req.ContentType = "application/json";
            req.Headers.Add("Authorization", basicAuthValue);

            Stream newStream = req.GetRequestStream();
            newStream.Write(bytesRequestBody, 0, bytesRequestBody.Length);
            newStream.Close();

            //Response
            HttpWebResponse response = (HttpWebResponse)req.GetResponse();
            if (response.StatusCode == HttpStatusCode.OK)
            {
                using (Stream stream = response.GetResponseStream())
                {
                    StreamReader reader = new StreamReader(stream, Encoding.UTF8);
                    string responseString = reader.ReadToEnd();

                    var responseTransaction = new JavaScriptSerializer().Deserialize<ResponseTransaction>(responseString);
                    if (responseTransaction.error == null || responseTransaction.error == "")
                    {
                        return responseTransaction.result;
                    }
                    else
                    {
                        Console.WriteLine("Response Error" + responseTransaction.error + " : " + jsonRequestBody);
                        throw new Exception();
                    }
                }
            }
            else
            {
                Console.WriteLine("HTTP Error" + response.StatusCode.ToString() + " : " + jsonRequestBody);
                throw new Exception();
            }

        }

        static bool InsertTransaction(Transaction transaction)
        {
            string cmdString = "INSERT INTO [Transaction] ([Txid],[BlockHash],[Version],[Time]) VALUES (@Txid, @BlockHash, @Version, @Time)";
            using (SqlConnection conn = new SqlConnection(connString))
            {

                using (SqlCommand comm = new SqlCommand())
                {
                    comm.Connection = conn;
                    comm.CommandText = cmdString;
                    comm.Parameters.AddWithValue("@Txid", transaction.txid);
                    comm.Parameters.AddWithValue("@BlockHash", transaction.blockhash);
                    comm.Parameters.AddWithValue("@Version", transaction.version);
                    comm.Parameters.AddWithValue("@Time", UnixTimeStampToDateTime(transaction.time));
                    try
                    {
                        conn.Open();
                        comm.ExecuteNonQuery();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("SQL Error" + ex.Message + " : " + new JavaScriptSerializer().Serialize(transaction));
                        throw;
                    }
                }
            }

            return true;
        }

        static bool InsertTransactionInput(TransactionInput transactionInput, string txid, int index)
        {
            string sAddress = "";
            float sValue = 0;

            if (transactionInput.coinbase != null)
            {
                sAddress = "0000000000000000000000000000000000";
            }
            else
            {
                string selectString = "SELECT [Address], [Value] FROM [TransactionOutput] WHERE [Txid] = '" + transactionInput.txid + "' AND [Index] = " + transactionInput.vout;
                using (SqlConnection conn = new SqlConnection(connString))
                {

                    using (SqlCommand comm = new SqlCommand(selectString, conn))
                    {
                        try
                        {
                            conn.Open();
                            using (SqlDataReader dr = comm.ExecuteReader())
                            {
                                while (dr.Read())
                                {
                                    sAddress = dr["Address"].ToString();
                                    sValue = float.Parse(dr["Value"].ToString());
                                }
                            }

                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("SQL Error" + ex.Message + " : " + new JavaScriptSerializer().Serialize(transactionInput) + " ; " + txid + " ; " + index.ToString());
                            throw;
                        }
                    }
                }
            }

            string cmdString = "INSERT INTO [TransactionInput] ([Txid],[Index],[Address],[Value]) VALUES (@Txid, @Index, @Address, @Value)";
            using (SqlConnection conn = new SqlConnection(connString))
            {

                using (SqlCommand comm = new SqlCommand())
                {
                    comm.Connection = conn;
                    comm.CommandText = cmdString;
                    comm.Parameters.AddWithValue("@Txid", txid);
                    comm.Parameters.AddWithValue("@Index", index);
                    comm.Parameters.AddWithValue("@Address", sAddress);
                    comm.Parameters.AddWithValue("@Value", sValue);
                    try
                    {
                        conn.Open();
                        comm.ExecuteNonQuery();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("SQL Error" + ex.Message + " : " + new JavaScriptSerializer().Serialize(transactionInput) + " ; " + txid + " ; " + index.ToString());
                        throw;
                    }
                }
            }

            return true;
        }

        static bool InsertTransactionOutput(TransactionOutput transactionOutput, string txid)
        {

            string cmdString = "INSERT INTO [TransactionOutput] ([Txid],[Index],[Address],[Value]) VALUES (@Txid, @Index, @Address, @Value)";
            using (SqlConnection conn = new SqlConnection(connString))
            {

                using (SqlCommand comm = new SqlCommand())
                {
                    comm.Connection = conn;
                    comm.CommandText = cmdString;
                    comm.Parameters.AddWithValue("@Txid", txid);
                    comm.Parameters.AddWithValue("@Index", transactionOutput.n);
                    comm.Parameters.AddWithValue("@Address", transactionOutput.scriptPubKey.addresses[0]);
                    comm.Parameters.AddWithValue("@Value", transactionOutput.value);
                    try
                    {
                        conn.Open();
                        comm.ExecuteNonQuery();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("SQL Error" + ex.Message + " : " + new JavaScriptSerializer().Serialize(transactionOutput) + " ; " + txid);
                        throw;
                    }
                }
            }

            return true;
        }


        public static DateTime UnixTimeStampToDateTime(double unixTimeStamp)
        {
            // Unix timestamp is seconds past epoch
            System.DateTime dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc);
            dtDateTime = dtDateTime.AddSeconds(unixTimeStamp);
            return dtDateTime;
        }


    }

    class RequestBody
    {
        public string method { get; set; }
        public object[] @params { get; set; }
    }

    class ResponseBlock
    {
        public Block result { get; set; }
        public string error { get; set; }
        public string id { get; set; }
    }

    class Block
    {
        public string hash { get; set; }
        public string pow_hash { get; set; }
        public int confirmations { get; set; }
        public int size { get; set; }
        public int height { get; set; }
        public int version { get; set; }
        public string merkleroot { get; set; }
        public string[] tx { get; set; }
        public int time { get; set; }
        public long nonce { get; set; }
        public string bits { get; set; }
        public float difficulty { get; set; }
        public string previousblockhash { get; set; }
        public string nextblockhash { get; set; }
    }

    class ResponseTransaction
    {
        public Transaction result { get; set; }
        public string error { get; set; }
        public string id { get; set; }
    }

    class Transaction
    {
        public string hex { get; set; }
        public string txid { get; set; }
        public int version { get; set; }
        public int locktime { get; set; }
        public TransactionInput[] vin { get; set; }
        public TransactionOutput[] vout { get; set; }
        public string blockhash { get; set; }
        public int confirmations { get; set; }
        public int time { get; set; }
        public int blocktime { get; set; }
    }

    class TransactionInput
    {
        public string coinbase { get; set; }
        public string txid { get; set; }
        public long vout { get; set; }
        public TransactionScriptSig scriptSig { get; set; }
        public long sequence { get; set; }
    }

    class TransactionOutput
    {
        public float value { get; set; }
        public int n { get; set; }
        public TransactionScriptPubKey scriptPubKey { get; set; }
    }

    class TransactionScriptSig
    {
        public string asm { get; set; }
        public string hex { get; set; }
    }

    class TransactionScriptPubKey
    {
        public string asm { get; set; }
        public string hex { get; set; }
        public int reqSigs { get; set; }
        public string type { get; set; }
        public string[] addresses { get; set; }
    }
}