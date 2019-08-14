pipeline {
    stages {
        stage('Checkout') {
            agent { label 'win-build' }
            steps {
                checkout scm
                stash name: 'Checkout'
            }
        }
    }
}