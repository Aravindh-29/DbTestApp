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
sudo dotnet publish -c Release -o /home/ubuntu/published

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



# JenkinsFile 


```
pipeline {
    agent any

    environment {
        SONAR_TOKEN = credentials('Sonar-token')   // available in all stages
    }

    stages {
        stage('SCM') {
            steps {
                git('https://github.com/Aravindh-29/DbTestApp.git')
            }
        }

        stage('SonarQube Analysis') {
            steps {
                withSonarQubeEnv('SonarServer') {
                    sh """
                        export PATH=\$PATH:\$HOME/.dotnet/tools
                        
                        dotnet sonarscanner begin \
                          /k:"nowshad13" \
                          /o:"nowshad13" \
                          /d:sonar.host.url="https://sonarcloud.io" \
                          /d:sonar.login=\$SONAR_TOKEN

                        dotnet build -c Release

                        dotnet sonarscanner end /d:sonar.login=\$SONAR_TOKEN
                    """
                }
            }
        }

        stage('Build') {
            steps {
                sh 'dotnet build DbConnectionTester.sln'
            }
        }

        stage('Test') {
            steps {
                sh 'dotnet test DbConnectionTester.sln'
            }
        }

        stage('Publish') {
            steps {
                sh 'dotnet publish DbConnectionTester.sln -c Release -o ./published'
            }
        }

        stage('Archive Artifacts') {
            steps {
                archiveArtifacts artifacts: 'published/**', fingerprint: true
            }
        }
    }
}

```
## Line-by-line explanation (simple)

* `withSonarQubeEnv('SonarServer')`

  * Tells Jenkins to use the Sonar server config named **SonarServer** (you set this in *Manage Jenkins → Configure System → SonarQube servers*).
  * Also helps `waitForQualityGate` (if used) to work later.

* `sh """ ... """`

  * Runs a bash script on the agent. Triple quotes are Groovy multiline strings.

* `export PATH=\$PATH:\$HOME/.dotnet/tools`

  * Ensures `dotnet-sonarscanner` (a global dotnet tool) can be found.
  * **Why `\$PATH` and not `$PATH`?** — inside a Jenkinsfile multiline string, Groovy would try to expand `$PATH`. Backslash `\` prevents Groovy from touching it so the shell sees `$PATH`. (In plain terminal you use `$PATH`.)

* `dotnet sonarscanner begin \`

  * **Starts** the Sonar analysis session. The flags that follow configure the analysis.

  Flags:

  * `/k:"nowshad13"` → **project key** inside Sonar (unique id for this project). **No spaces** after `:` — must be `/k:"..."`.
  * `/o:"nowshad13"` → **organization** in SonarCloud **(only for SonarCloud)**. Remove this for self-hosted SonarQube.
  * `/d:sonar.host.url="https://sonarcloud.io"` → explicit host url. Important because newer scanners default to SonarCloud. For self-hosted SonarQube use `http://localhost:9000` (or your server URL).
  * `/d:sonar.login=\$SONAR_TOKEN` → authentication token. We pass the token from Jenkins credentials (escape `\$` so Groovy doesn't expand it).

* `dotnet build -c Release`

  * Builds the project in **Release** configuration. The Sonar scanner hooks into MSBuild during this build and collects analysis data.
  * **Important:** run this in the same folder where your `.sln` or `.csproj` is. You can specify solution explicitly: `dotnet build DbConnectionTester.sln -c Release`.

* `dotnet sonarscanner end /d:sonar.login=\$SONAR_TOKEN`

  * **Ends** the analysis session and uploads results to Sonar.



