using Constellation;
using Constellation.Package;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Brain
{
    public class Program : PackageBase
    {
        // Crée un lien sur le S.O. etatSonnette
        [StateObjectLink("SerrurePackage", "etatSonnette")]
        private StateObject Sonnette { get; set; }

        const string SUBSCRIPTION_KEY = "MICROSOFT_AZURE_SUBSCRIPTION_KEY";
        const string uriBase ="https://westeurope.api.cognitive.microsoft.com/face/v1.0/detect";
        const string FIND_FACEID = "faceId";
        const string FIND_ISIDENTICAL = "isIdentical";

        public class Result_face_verify
        {
            public Boolean IsIdentical { get; set; }
            public double Confidence { get; set; }
        }

        static void Main(string[] args)
        {
            PackageHost.Start<Program>(args);
            Console.WriteLine("Press any key to Stop...");
            Console.ReadKey();
        }

        public override void OnStart()
        {
            PackageHost.WriteInfo("Package starting - IsRunning: {0} - IsConnected: {1}", PackageHost.IsRunning, PackageHost.IsConnected);
            PackageHost.WriteInfo("je demarre le brain");
            
            // A chaque MàJ du S.O. etatSonnette on regarde sa valeur
            PackageHost.StateObjectUpdated += (s, e) =>
            {
                if (e.StateObject.Name == "etatSonnette")
                {
                    if (e.StateObject.DynamicValue["etat"] == 1)
                    {
                        PackageHost.WriteInfo("Screenshot !!!");
                        PackageHost.WriteInfo("Name : " + e.StateObject.Name);
                        PackageHost.CreateMessageProxy("Constellation_Screenshot").Screenshot();
                        AnalysePhotosAsync();
                    }
                }
            };
        }

        /// <summary>
        /// Gère le processus de la reconnaissance d'image
        /// </summary>
        static async void AnalysePhotosAsync()
        {
            bool keep_going = false;
            PackageHost.WriteInfo("--------------------- Face - Detect ---------------------\n");
            Console.WriteLine("--------------------- Face - Detect ---------------------\n");

            // Obtention des faceID des photos "témoins"
            string[] fichiersTemoins = Directory.GetFiles(@"D:\images\Photos_Temoins");
            string[] listeFaceIDTemoins = await FaceIDPhotosTemoins(fichiersTemoins);
            
            // Obtention du faceID de la photo qui vient d'être prise
            string imageFilePath = @"D:\images\image.jpg";
            string faceId2 = await FaceIDAsync(imageFilePath);
            
            // Si y'a un bien un faceID on peut continuer l'analyse
            if (faceId2 != "")
            {
                Console.WriteLine("\nfaceId2 : " + faceId2 + "\n");
                keep_going = true;
            }
            // Sinon on affiche un message
            else Console.WriteLine("\nfaceId2 invalide\n");

            if (keep_going)
            {
                bool samePerson = false;
                PackageHost.WriteInfo("--------------------- Face - Verify ---------------------\n");
                Console.WriteLine("--------------------- Face - Verify ---------------------\n");
                int i = 0;
                // Envoi à Cognitives Services des faceID de chaque photo témoin avec le faceID de la photo prise
                while (i < listeFaceIDTemoins.Length && !samePerson)
                {
                    Result_face_verify verif = await MakeRequestFaceVerify(listeFaceIDTemoins[i], faceId2);
                    PackageHost.WriteInfo("\nVerification :\nisIdentical : " + verif.IsIdentical + ", condifence : " + Convert.ToString(verif.Confidence) + "\n\n\n");
                    Console.Write("\nVerification :\nisIdentical : " + verif.IsIdentical + ", condifence : " + Convert.ToString(verif.Confidence) + "\n\n\n");
                    
                    // Si la personne qui a été prise en photo est dans les photos témoins, on ouvre la porte
                    if (verif.IsIdentical == true)
                    {
                        samePerson = true;
                        PackageHost.CreateMessageProxy("SerrurePackage").OpenDoor();
                    }
                    i++;
                }
                // S'il n'y a pas eu de correspondance, envoi de la photo avec PushBullet
                if (!samePerson)
                {
                    Console.Write("\nPersonne autorisee non reconnue\n");
                    PackageHost.CreateMessageProxy("PushBullet").PushFile(@"D:\Images\image.jpg", "Ca sonne !", "Device");
                }
            }
            else
            {
                Console.Write("\nFace - Verify impossible, faceId non valide\n");
                PackageHost.WriteInfo("\nMessage envoyé sur PushBullet");
                PackageHost.CreateMessageProxy("PushBullet").PushFile(@"D:\Images\image.jpg", "Ca sonne !", "Device");
            }
        }


        /// <summary>
        /// Recupère les faceID des photos "témoins" sous forme de tableau
        /// </summary>
        /// <param name="fichiers"></param>
        /// <returns>Tableau de string contenant les faceID des photos témoins</returns>
        static async Task<string[]> FaceIDPhotosTemoins(string[] fichiers)
        {
            int nbFiles = fichiers.Length;
            string[] FaceID = new string[nbFiles];
            Console.Write(nbFiles + " fichiers trouvés\n");
            for (int i = 0; i < nbFiles; i++)
            {
                //Console.WriteLine(fichiers[i]);
                FaceID[i] = await FaceIDAsync(fichiers[i]);
            }

            return FaceID;
        }

        /// <summary>
        /// Récupère le faceID d'une photo
        /// </summary>
        /// <param name="imageFilePath"></param>
        /// <returns>faceID</returns>
        static async Task<string> FaceIDAsync(string imageFilePath)
        {
            PackageHost.WriteInfo("\n\n\nPath of the image : " + imageFilePath);
            Console.Write("\n\n\nPath of the image : " + imageFilePath);
            string faceId;
            string faceId_String = "";

            // Si le fichier existe, on récupère le faceID
            if (File.Exists(imageFilePath))
            {
                // Execute the REST API call.
                try
                {
                    faceId = await MakeFaceAnalysisRequest(imageFilePath);
                    faceId_String = Convert.ToString(faceId);
                }
                catch (Exception e)
                {
                    Console.WriteLine("\n" + Convert.ToString(e.Message) + "\nPress Enter to exit...\n");
                }
            }
            // Sinon, affichage d'un message d'erreur
            else
            {
                Console.WriteLine("\nInvalid file path.\nPress Enter to exit...\n");
            }
            return faceId_String;
        }


        /// <summary>
        /// Utilise la Face REST API pour avoir l'analyse d'une image spécifiée et obtenir le faceID
        /// </summary>
        /// <param name="imageFilePath">The image file.</param>
        private static async Task<string> MakeFaceAnalysisRequest(string imageFilePath)
        {
            HttpClient client = new HttpClient();

            // Request headers.
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", SUBSCRIPTION_KEY);

            // Request parameters. A third optional parameter is "details".
            string requestParameters = "returnFaceId=true&returnFaceLandmarks=false" +
                "&returnFaceAttributes=age,gender";

            // Assemble the URI for the REST API Call.
            string uri = uriBase + "?" + requestParameters;

            HttpResponseMessage response;

            // Request body. Posts a locally stored JPEG image.
            byte[] byteData = GetImageAsByteArray(imageFilePath);

            using (ByteArrayContent content = new ByteArrayContent(byteData))
            {
                content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

                // Execute the REST API call.
                response = await client.PostAsync(uri, content);

                // Get the JSON response.
                string contentString = await response.Content.ReadAsStringAsync();

                // Display the JSON response.
                Console.WriteLine("\nResponse:");
                Console.Write(contentString);
                
                // Recherche du faceID dans la réponse 
                string faceId = SearchFaceId(contentString, FIND_FACEID);
                return faceId;
            }
        }


        /// <summary>
        /// Retourne le contenu d'une image sous forme de byte array
        /// </summary>
        /// <param name="imageFilePath">The image file to read.</param>
        /// <returns>Le byte array des données de l'image.</returns>
        static byte[] GetImageAsByteArray(string imageFilePath)
        {
            using (FileStream fileStream =
                new FileStream(imageFilePath, FileMode.Open, FileAccess.Read))
            {
                BinaryReader binaryReader = new BinaryReader(fileStream);
                return binaryReader.ReadBytes((int)fileStream.Length);
            }
        }


        /// <summary>
        /// Cherche le faceID dans un string
        /// </summary>
        /// <param name="source"></param>
        /// <param name="recherche"></param>
        /// <returns>La valeur du faceID en string</returns>
        static string SearchFaceId(string source, string recherche)
        {
            bool find = false;
            int lenSource = source.Length;
            string buffer = "";
            int indexFaceId = source.IndexOf(recherche); // -1 si il n'y est pas

            if (indexFaceId != -1)
            {
                int newIndex = indexFaceId + 9;  // "faceId":"key"
                while (!find && newIndex < lenSource)
                {
                    if (source[newIndex] != '\"')
                    {
                        buffer += source[newIndex];
                        newIndex++;
                    }
                    else find = true;
                }
            }
            return buffer;
        }


        /// <summary>
        /// Utilse la Face REST API pour savoir si une personne est sur les 2 photos spécifiées
        /// </summary>
        /// <param name="faceId1"></param>
        /// <param name="faceId2"></param>
        /// <returns>Le resultat de la vérification : isIdentical et confidence</returns>
        static async Task<Result_face_verify> MakeRequestFaceVerify(string faceId1, string faceId2)
        {
            var client = new HttpClient();

            // Request headers
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", SUBSCRIPTION_KEY);

            var uri = "https://westeurope.api.cognitive.microsoft.com/face/v1.0/verify";

            HttpResponseMessage response;
            // Request body
            String body = "{'faceId1':'" + faceId1 + "','faceId2':'" + faceId2 + "'}";
            byte[] byteData = Encoding.UTF8.GetBytes(body);
            Result_face_verify faceVerify;

            using (var content = new ByteArrayContent(byteData))
            {
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                response = await client.PostAsync(uri, content);
                var responseString = await response.Content.ReadAsStringAsync();
                // Affichage de la réponse
                Console.Write("Response : " + responseString);
                // Recherche des éléments "isIdentical" et "confidence" dans la réponse
                faceVerify = SearchFaceVerify(responseString, FIND_ISIDENTICAL);
            }
            return faceVerify;
        }


        /// <summary>
        /// Cherche le résultat de la Face - Verify dans un string
        /// </summary>
        /// <param name="source"></param>
        /// <param name="recherche"></param>
        /// <returns>isIdentical et confidence</returns>
        static Result_face_verify SearchFaceVerify(string source, string recherche)
        {
            bool find = false;
            int lenSource = source.Length;
            string bufferIsIdentical = "";
            
            int indexIsIdentical = source.IndexOf(recherche); // -1 si il n'y est pas

            Result_face_verify resultat = new Result_face_verify { };

            if (indexIsIdentical != -1)
            {
                int newIndex = indexIsIdentical + 13;  // "isIdentical":true,  ou  "isIdentical":false,
                while (!find && newIndex < lenSource)
                {
                    if (source[newIndex] != ',')
                    {
                        bufferIsIdentical += source[newIndex];
                        newIndex++;
                    }
                    else find = true;
                }

                // {"isIdentical":false,"confidence":0.18558}
                int indexConfidence = source.IndexOf("confidence");
                if (indexConfidence != -1)
                {
                    newIndex = indexConfidence + 12;
                    find = false;
                    string bufferConfidence = "";
                    while (!find && newIndex < lenSource)
                    {
                        if (source[newIndex] != '}')
                        {
                            bufferConfidence += source[newIndex];
                            newIndex++;
                        }
                        else find = true;
                    }
                    resultat.IsIdentical = Convert.ToBoolean(bufferIsIdentical);
                    resultat.Confidence = float.Parse(bufferConfidence, CultureInfo.InvariantCulture.NumberFormat);
                }
            }
            return resultat;
        }

    }
}