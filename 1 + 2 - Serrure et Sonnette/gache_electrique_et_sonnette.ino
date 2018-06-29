// Importation des bibliothèques pour l'ESP et Constellation
#include <Constellation.h>
#include <ESP8266WiFi.h>

char ssid[] = "MY_SSID";
char password[] = "My_Password";


//Déclaration des pins sur l'ESP
#define LOCK_PIN D3
#define BP D1
#define LED D2

// Constellation client
Constellation<WiFiClient> constellation("IP Serveur", port, "Sentinelle", "Package", "Clé Constellation");
 
void setup(void) {
  Serial.begin(115200);  
  delay(10);
 
  pinMode(LOCK_PIN, OUTPUT);
  pinMode(BP,INPUT);
  pinMode(LED,OUTPUT);
  // Initialisation
  digitalWrite(LOCK_PIN , LOW);
  digitalWrite(BP,LOW);
  digitalWrite(LED,LOW);

  
  // Connection au WiFi
  Serial.print("Connecting to ");
  Serial.println(ssid);  
  WiFi.begin(ssid, password);  
  while (WiFi.status() != WL_CONNECTED) {
    delay(500);
    Serial.print(".");
  }
  Serial.println("WiFi connected. IP: ");
  Serial.println(WiFi.localIP());
 

  
  // Publication de StateObject
  constellation.pushStateObject("etatSonnette", "{'etat':0}","DATA",10);
  constellation.pushStateObject("Serrure","{'etat':0}","DATA");  
  constellation.addStateObjectType("DATA", TypeDescriptor().setDescription("My sonnette").addProperty("etat", "System.Int32"));
  constellation.declarePackageDescriptor();

// Publication de messages callback
  constellation.registerMessageCallback("OpenDoor", MessageCallbackDescriptor().setDescription("Ouvre la porte"),
      [](JsonObject& message) {
            digitalWrite(LOCK_PIN, HIGH);
            digitalWrite(LED,HIGH);
            constellation.pushStateObject("Serrure","{'etat':1}","DATA");
            delay(5000);
            digitalWrite(LOCK_PIN, LOW);
            digitalWrite(LED,LOW);
            constellation.pushStateObject("Serrure","{'etat':0}","DATA");
      });
  
  constellation.declarePackageDescriptor();

  
  
 
  // WriteLog info
  constellation.writeInfo("Virtual Package on '%s' is started !", constellation.getSentinelName());  
}
 
void loop(void) {
  constellation.loop();

//Code pour la sonnette
  if(digitalRead(BP)==HIGH){
    constellation.writeInfo("Le BP est a l'etat haut");
    constellation.pushStateObject("etatSonnette","{'etat':1}","DATA");
    delay(1000);
  }
  else constellation.pushStateObject("etatSonnette","{'etat':0}","DATA");
  
}