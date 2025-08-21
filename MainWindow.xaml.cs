using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace CacheApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Socket cacheSocket, clientSocket, serverSocket;
        private const int CACHE_PORT = 8087;
        private const int SERVER_PORT = 8088;
        private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        public MainWindow()
        {
            InitializeComponent();
            fragmentList.SelectionMode = SelectionMode.Single;
            logDisplay.IsReadOnly = true;
            try
            {
                // instantiate server and cache socket to allow connection and listen of server and client respectively
                serverSocket = CreateConnectingSocket(SERVER_PORT);
                cacheSocket = CreateListeningSocket(CACHE_PORT);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error in Cache: " + ex.Message);
            }
            // Display fragments stored in cache directory 
            closeCacheBtn.IsEnabled = true;
            DisplayFragmentFunction();

            // Run tasks to handle server and client related task independently 
            Task.Run(() => HandleServerTask(cancellationTokenSource.Token));
            Task.Run(() => HandleClientTask(cancellationTokenSource.Token));
        }

        private string GetCurrentTime()
        {
            return DateTime.Now.ToString("dd-MM-yyy HH:mm:ss");
        }

        private void fragmentList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            viewFragmentBtn.IsEnabled = fragmentList.SelectedItems.Count > 0;
        }

        // Create and return a connecting socket to the specified port number
        private Socket CreateConnectingSocket(int port)
        {
            Socket sck = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Loopback, port);
            sck.Connect(endPoint);
            return sck;
        }

        // Create and return a listening socket to the specified port number 
        private Socket CreateListeningSocket(int port)
        {
            Socket sck = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Loopback, port);
            sck.Bind(endPoint);
            sck.Listen(0);
            return sck;
        }

        private void DisplayFragmentFunction()
        {
            // Retrieves the data files stored in the Fragments folder of the cache directory and display the fragment data file in GUI 
            DirectoryInfo directoryInfo = new DirectoryInfo(@".\Cache\CachedFragments");
            if (!directoryInfo.Exists)
            {
                Directory.CreateDirectory(@".\Cache\CachedFragments");
            }
            FileInfo[] dataFileArray = directoryInfo.GetFiles("*.dat");
            var dataFileArraySorted = dataFileArray.OrderBy(f => int.Parse(System.IO.Path.GetFileNameWithoutExtension(f.Name)));
            foreach (FileInfo dataFile in dataFileArraySorted)
            {
                fragmentList.Items.Add(dataFile.Name);
            }
            fragmentList.IsEnabled = true;
            clearFragmentBtn.IsEnabled = true;
        }

        private async void HandleClientTask(CancellationToken cancellationToken)
        {
            // Accept incoming connection associated with the cache socket,
            clientSocket = await cacheSocket.AcceptAsync();
            byte[] buffer = new byte[1024 * 2]; // Assuming that the received request from client socket is no more than 2048 bytes 
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    int receivedByteLength = clientSocket.Receive(buffer);
                    string receivedString = Encoding.Default.GetString(buffer, 0, receivedByteLength);
                    // Once request from client is received then judge to see what type of request it is to allow appropriate handling of request
                    if (receivedString.StartsWith("selectedFile;"))
                    {
                        // When client has selected to download a file from server
                        string fileName = receivedString.Substring(13);
                        _ = Dispatcher.BeginInvoke(() =>
                        {
                            // Update the cache log to store user activity to cache 
                            string msg = "User requested: " + fileName + " at " + GetCurrentTime();
                            UpdateCacheLog(msg); //**********************************
                        });
                        // Cache will send a request to the server to obtain the data fragment list that composes the selected file 
                        // the data fragment list of a file keeps a record of all the data fragments that constitute for the file and is constructed upon uploading of the file from the server side
                        byte[] reqToServer = Encoding.Default.GetBytes("getFragList;" + fileName);
                        serverSocket.Send(reqToServer);


                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error in Cache: " + ex.Message);
                    break;
                }
            }
            clientSocket.Close();
        }

        private void UpdateCacheLog(string record)
        {
            string cacheLogPath = System.IO.Path.Combine(@".\Cache\cacheLog.txt");
            // Create file if not exist 
            if (!File.Exists(cacheLogPath))
            {
                File.Create(cacheLogPath).Close();
            }
            // Append the new record of user activity to the cache log text file and update the cahce log 
            File.AppendAllText(cacheLogPath, record + "\r\n");
            logDisplay.AppendText(record + "\r\n");

        }

        private void HandleFragmentListReceived(string fragmentList)
        {
            // Receive the data corresponding for the fragment list length and convert the data received into integer rocess fragment list length
            byte[] fragmentListLengthBytes = new byte[16];
            serverSocket.Receive(fragmentListLengthBytes, 0, fragmentListLengthBytes.Length, SocketFlags.None);
            int fragmentListLength = BitConverter.ToInt32(fragmentListLengthBytes);
            // Receive the fragment list file content send from server socket and write the data into a memory stream object 
            byte[] buffer = new byte[1024 * 1024];
            MemoryStream receivedFragmentFile = new MemoryStream();
            int bytesProessed;
            int totalBytesProcessed = 0;
            do
            {
                bytesProessed = serverSocket.Receive(buffer);
                receivedFragmentFile.Write(buffer, 0, bytesProessed);
                totalBytesProcessed += bytesProessed;
            } while (totalBytesProcessed != fragmentListLength);

            // Encoding the data received, stored in a memory stream object into string and splitting the string into array according to specified pattern
            string receivedData = Encoding.Default.GetString(receivedFragmentFile.ToArray());
            string[] fileFragments = receivedData.Split(" ");

            // Call CalculateFileCompleteness() method to figure what fragments are missing in the cache for the construction of the file 
            string[] missingFragments = null;
            Dispatcher.Invoke(() =>
            {
                missingFragments = CalculateFileCompleteness(fileFragments, fragmentList);
            });


            // If there are missing data fragments, then request needs to be sent to the server to retreive the fragment 
            if (missingFragments.Length > 0)
            {
                Dispatcher.Invoke(() =>
                {
                    foreach (string fragment in missingFragments)
                    {
                        HandleMissingFragment(fragment, fragmentListLengthBytes);
                    }
                });
            }

            Dispatcher.Invoke(() =>
            {
                DisplayFragmentFunction();
                CombineSendImage(fileFragments);
            });

        }

        private string[] CalculateFileCompleteness(string[] fileFragments, string fragmentList)
        {
            // Retrieve all .dat files sotred inside tha cahce Fragments folder 
            string[] fragmentPath = Directory.GetFiles(@".\Cache\CachedFragments", "*.dat");

            // Retreive the data fragment number from the Fragments folder in cache
            string[] storedFragments = new string[fragmentPath.Length];
            for (int i = 0; i < fragmentPath.Length; i++)
            {
                storedFragments[i] = System.IO.Path.GetFileNameWithoutExtension(fragmentPath[i]);
            }
            // To find any missing fragments by comparing between storedFragments array and fragmentArray of the file 
            List<string> missingFragmentsList = new List<string>();
            List<string> fileFragmentsNoDuplicate = new List<string>();
            foreach (string fragment in fileFragments)
            {
                if (!storedFragments.Contains(fragment)) // If fragment data file is not stored in or exist in local directory 
                {
                    if (!missingFragmentsList.Contains(fragment)) // If the missing file list hasnt already recorded the missing data fragment number
                    {
                        missingFragmentsList.Add(fragment);
                    }
                }
                if (!fileFragmentsNoDuplicate.Contains(fragment))
                {
                    fileFragmentsNoDuplicate.Add(fragment);
                }
            }
            string[] missingFragments = missingFragmentsList.ToArray();
            string[] fragmentListNoDuplicate = fileFragmentsNoDuplicate.ToArray();

            // Caculate the percentage of data constructed with fragments stored in cache and update cache log
            double percentageUsed = (1 - ((double)missingFragments.Length / fragmentListNoDuplicate.Length)) * 100;
            string record = "Response: " + string.Format("{0:0.##}", percentageUsed) + "% of file, " + fragmentList + " is constructed with existing data stored in cache";
            UpdateCacheLog(record);
            return missingFragments;
        }

        private void HandleMissingFragment(string fragment, byte[] fragmentListLengthBytes)
        {
            // Send data request for specified fragment to server 
            byte[] fragmentRequest = Encoding.Default.GetBytes("getFragment;" + fragment);
            serverSocket.Send(fragmentRequest);

            // Receive data from server corresponding to the fragment file length and convert that data received into integer
            serverSocket.Receive(fragmentListLengthBytes, 0, fragmentListLengthBytes.Length, SocketFlags.None);
            int fragFileLength = BitConverter.ToInt32(fragmentListLengthBytes);

            // Receive data corresponding to the fragment content and write the received data to the created memory stream object
            byte[] fragmentBuffer = new byte[4096];
            MemoryStream fragmentFile = new MemoryStream();
            int bytesProcessed;
            int totalBytesProcessed = 0;
            while (totalBytesProcessed < fragFileLength)
            {
                bytesProcessed = serverSocket.Receive(fragmentBuffer, 0, fragmentBuffer.Length, SocketFlags.None);
                fragmentFile.Write(fragmentBuffer, 0, bytesProcessed);
                totalBytesProcessed += bytesProcessed;
            }
            string storagePath = System.IO.Path.Combine(@".\Cache\CachedFragments", fragment + ".dat");
            File.WriteAllBytes(storagePath, fragmentFile.ToArray());
        }

        private void CombineSendImage(string[] fragmentArray)
        {
            // Create a memory strea object which will store the data from the fragmentArray 
            MemoryStream imageStream = new MemoryStream();
            foreach (string fragName in fragmentArray)
            {
                // Retreiving the fragment data stored inside cache and writing this data into the image memory strea object 
                string fragPath = System.IO.Path.Combine(@".\Cache\CachedFragments", fragName + ".dat");
                byte[] fragData = File.ReadAllBytes(fragPath);
                imageStream.Write(fragData, 0, fragData.Length);
            }
            // Convert image memory stream object into bytes and then send to client 
            byte[] combinedImageData = imageStream.ToArray();
            byte[] imageLengthBytes = BitConverter.GetBytes(combinedImageData.Length);
            Thread.Sleep(1000);
            clientSocket.Send(imageLengthBytes, 0, imageLengthBytes.Length, SocketFlags.None);
            clientSocket.Send(combinedImageData, 0, combinedImageData.Length, SocketFlags.None);
        }

        private void viewLogBtn_Click(object sender, RoutedEventArgs e)
        {
            string cacheLogPath = System.IO.Path.Combine(@".\Cache\cacheLog.txt");
            // Check to see if cacheLog.txt fie exist, if not then create 
            if (!File.Exists(cacheLogPath))
            {
                File.Create(cacheLogPath).Close();
            }
            // To open the cacheLog.txt file 
            try
            {
                ProcessStartInfo processStartInfo = new ProcessStartInfo(cacheLogPath)
                {
                    UseShellExecute = true,
                    Verb = "open"
                };
                Process.Start(processStartInfo);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error in Cache: (error in opening cache log file) {ex.Message}");
            }
        }

        private void clearLogBtn_Click(object sender, RoutedEventArgs e)
        {
            string cacheLogPath = System.IO.Path.Combine(@".\Cache\cacheLog.txt");
            // Verify and confirm clearing request 
            if (File.Exists(cacheLogPath))
            {
                MessageBoxResult result = MessageBox.Show("Cache log clearance", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question);
                // If clearance of cache log is confirmed then delete the file 
                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        File.WriteAllText(cacheLogPath, "");
                        logDisplay.Clear();
                        MessageBox.Show("Cache log has been cleared");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Error in Cache: (unable to clear cache log) " + ex.Message);
                    }
                }
            }
            else
            {
                MessageBox.Show("Cache log file does not exist");
            }
        }

        private void viewFragmentBtn_Click(object sender, RoutedEventArgs e)
        {
            // Obtain the selected fragment item from the fragmentList and get the associated fragment path
            string selectedItem = fragmentList.SelectedItem.ToString();
            string fragmentPath = System.IO.Path.Combine(@".\Cache\CachedFragments", selectedItem);
            // Verify if the fragment data file exist in Fragments file in cache
            if (File.Exists(fragmentPath))
            {
                // Retrieve the fragment content by reading the data file and convert the bytes retreived into hexadecimal string
                byte[] fragmentContent = File.ReadAllBytes(fragmentPath);
                // Convert the file content to a hexadecimal string
                StringBuilder stringBuilder = new StringBuilder(fragmentContent.Length * 2);
                foreach (byte b in fragmentContent)
                {
                    stringBuilder.AppendFormat("{0:x2}", b);
                }
                string hexaString = stringBuilder.ToString();
                MessageBox.Show(hexaString);
            }
            else
            {
                MessageBox.Show($"Error in Cache: '{selectedItem}' data fragment not found");
            }
        }

        private void closeCacheBtn_Click(object sender, RoutedEventArgs e)
        {
            cancellationTokenSource.Cancel();
            this.Close();
        }

        private void clearFragmentBtn_Click(object sender, RoutedEventArgs e)
        {
            string folderPath = @".\Cache\CachedFragments";
            DirectoryInfo fragmentsFolder = new DirectoryInfo(folderPath);
            if (fragmentsFolder.Exists)
            {
                try
                {
                    // Delete all files in the folder
                    foreach (FileInfo fragment in fragmentsFolder.GetFiles())
                    {
                        fragment.Delete();
                    }

                    // Update the GUI which displays the data fragments stored in cache
                    fragmentList.Items.Clear();
                    string record = "All fragment data files in cache had been cleared as at " + GetCurrentTime();
                    UpdateCacheLog(record);
                    MessageBox.Show("Data fragments has been cleared successfully.");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error in Cache: (error in clearing fragments) {ex.Message}");
                }
            }
            else
            {
                MessageBox.Show($"Error in Cache: folder, '{folderPath}' does not exist");
            }
        }

        private async void HandleServerTask(CancellationToken cancellationToken)
        {
            byte[] buffer = new byte[1024 * 2];

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Receive response data from server and encode the received response into string and use the string to determine how to handle the received data 
                    int receivedByteLength = serverSocket.Receive(buffer);
                    string receivedString = Encoding.Default.GetString(buffer, 0, receivedByteLength);

                    if (receivedString.StartsWith("fragList;"))
                    {
                        // If a fragment list data is received then call HandleFragmentListReceived() which will judge where all data fragments exist in cache Fragments folder before sending it back to client
                        // If data fragments are missing then a getFragment; request will be sent to the server 
                        string fragmentList = receivedString.Substring(9);
                        Dispatcher.Invoke(() =>
                        {
                            HandleFragmentListReceived(fragmentList);
                        });
                    }
                    else if (receivedString.StartsWith("fileList;") && clientSocket != null)
                    {   
                        clientSocket.Send(Encoding.Default.GetBytes(receivedString));
                        _ = Dispatcher.BeginInvoke(() =>
                        {
                            string record = "File list on server was updated at " + GetCurrentTime();
                            UpdateCacheLog(record);
                        });

                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error in Cache: " + ex.Message);
                    break;
                }
            }
            serverSocket.Close();

        }
    }


}


