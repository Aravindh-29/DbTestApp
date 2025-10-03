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
## Line-by-line explanation (SONAR STAGE)

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

# Nexus Repository Manager

---

# 🔹 1. Overview of Nexus

Nexus Repository (by **Sonatype**) is a **repository manager**.
Think of it like a **storage hub** where you keep your build artifacts, Docker images, dependencies, and libraries.

👉 Simple gaa cheppali ante:

* GitHub = code repo
* Jenkins = build tool
* Nexus = artifacts repo (built files, jars, dlls, docker images, etc.)

---

## 🔹 2. Why we need Nexus?

1. **Central storage** → Store artifacts (JARs, WARs, DLLs, NuGet packages, Docker images).
2. **Dependency management** → Developers can download libraries from Nexus instead of internet.
3. **CI/CD integration** → Jenkins builds → uploads artifacts → Nexus stores them.
4. **Security** → You control which artifacts are allowed.
5. **Caching** → Speeds up builds by caching Maven, npm, NuGet, etc.

---

## 🔹 3. Types of repositories in Nexus

* **Hosted** → Store your own artifacts (e.g., JARs, Docker images, NuGet packages).
* **Proxy** → Cache remote repos (like Maven Central, npmjs, NuGet Gallery).
* **Group** → Combine multiple repos into one URL (easy for developers).

---

## 🔹 4. Nexus Editions

* **Nexus Repository OSS (Free)** → open source, supports many formats (Maven, npm, NuGet, Docker, etc.).
* **Nexus Pro (Paid / Free Trial)** → enterprise features like staging, advanced security, support.

👉 For learning/POC → **OSS free version is enough** (runs on your EC2/Docker).
👉 For trial → You can request Pro trial here:
🔗 [Sonatype Nexus Repository Free Trial](https://www.sonatype.com/products/sonatype-nexus-repository/free-trial)

---

## 🔹 5. How to run Nexus locally (free OSS)

Run Nexus in Docker (easiest way):

```bash
docker run -d -p 8081:8081 --name nexus sonatype/nexus3
```

* Access UI at → `http://<your-server-ip>:8081`
* Default user: `admin`
* Password: in container at `/nexus-data/admin.password`

---

## 🔹 6. How Nexus works with Jenkins

The typical CI/CD flow is:

1. **Build app** in Jenkins (`dotnet publish`, `mvn package`, `docker build`, etc.)
2. **Upload artifact** to Nexus repo using Jenkins.

   * Example: upload `.zip`, `.dll`, `.jar`, or Docker image.
3. **Later** → Developers or deployment scripts pull artifact from Nexus for deployment.

---

## 🔹 7. Jenkins Pipeline + Nexus Example

### Case 1: Upload a **ZIP/Build Artifact**

```groovy
stage('Upload to Nexus') {
    steps {
        nexusPublisher nexusInstanceId: 'nexus-server',
                       nexusRepositoryId: 'my-hosted-repo',
                       packages: [
                         [
                           $class: 'MavenPackage',
                           mavenAssetList: [
                             [classifier: '', extension: 'zip', filePath: 'published/myapp.zip']
                           ],
                           mavenCoordinate: [
                             artifactId: 'dbtestapp',
                             groupId: 'com.aravindh',
                             packaging: 'zip',
                             version: '1.0.0'
                           ]
                         ]
                       ]
    }
}
```

👉 Here:

* `nexusInstanceId` = the Nexus server config ID in Jenkins (set in **Manage Jenkins → Nexus Configuration**).
* `nexusRepositoryId` = repo name (e.g., `releases`, `snapshots`, or your own).
* `filePath` = artifact path (like `published/myapp.zip`).

---

### Case 2: Push a **Docker Image** to Nexus

If Nexus repo is configured as Docker registry:

```groovy
stage('Build & Push Docker') {
    steps {
        sh """
            docker build -t nexus-server:8082/dbtestapp:1.0.0 .
            docker login nexus-server:8082 -u admin -p $NEXUS_PASS
            docker push nexus-server:8082/dbtestapp:1.0.0
        """
    }
}
```

---



