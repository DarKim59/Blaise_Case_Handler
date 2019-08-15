pipeline {
    stages {
        stage('Checkout') {
           agent { label 'download.jenkins.slave' }
           steps {
               checkout scm
               script {
                   buildInfo.name = "${SVC_NAME}"
                   buildInfo.number = "${BUILD_NUMBER}"
                   buildInfo.env.collect()
               }
               colourText("info", "BuildInfo: ${buildInfo.name}-${buildInfo.number}")
               stash name: 'Checkout'
           }
       }
    }
}