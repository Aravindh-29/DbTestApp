`dotnet DbConnectionTester.dll --urls "http://0.0.0.0:5000"`

```
#!/bin/bash

# --------------------------
# 1. Update and Upgrade OS
# --------------------------
sudo apt update -y && sudo apt upgrade -y

# --------------------------
# 2. Install Git and .NET SDK 8
# --------------------------
sudo apt install -y git wget
wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
sudo apt update -y
sudo apt install -y dotnet-sdk-8.0

# --------------------------
# 3. Clone Your Git Repo
# --------------------------
cd /home/ubuntu
if [ ! -d "DbTestApp" ]; then
  git clone https://github.com/Aravindh-29/DbTestApp.git
fi
cd DbTestApp

# --------------------------
# 4. Publish the App
# --------------------------
dotnet publish -c Release -o /home/ubuntu/published

# --------------------------
# 5. Create systemd Service
# --------------------------
sudo tee /etc/systemd/system/dbconnection.service > /dev/null <<EOL
[Unit]
Description=DbConnectionTester .NET App
After=network.target

[Service]
WorkingDirectory=/home/ubuntu/published
ExecStart=/usr/bin/dotnet /home/ubuntu/published/DbConnectionTester.dll --urls "http://0.0.0.0:5000"
Restart=always
RestartSec=10
User=ubuntu
Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false

[Install]
WantedBy=multi-user.target
EOL

# Reload systemd, enable and start service
sudo systemctl daemon-reload
sudo systemctl enable dbconnection.service
sudo systemctl start dbconnection.service

echo "✅ Setup complete! App is running and accessible via http://<EC2-Public-IP>:5000"

```
