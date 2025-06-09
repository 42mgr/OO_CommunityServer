#!/bin/bash

# Docker Integration Script for CRM Email Monitoring
# This script helps integrate the CRM monitoring with your Docker setup

set -e

echo "🚀 ONLYOFFICE CRM Email Monitoring - Docker Integration"
echo "======================================================="

# Configuration
CONTAINER_NAME="onlyoffice-community-server"
DB_CONTAINER="onlyoffice-mysql-server"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

print_status() {
    echo -e "${BLUE}ℹ️  $1${NC}"
}

print_success() {
    echo -e "${GREEN}✅ $1${NC}"
}

print_warning() {
    echo -e "${YELLOW}⚠️  $1${NC}"
}

print_error() {
    echo -e "${RED}❌ $1${NC}"
}

# Function to check if container exists and is running
check_container() {
    local container=$1
    if docker ps --format "table {{.Names}}" | grep -q "^${container}$"; then
        return 0
    else
        return 1
    fi
}

# Function to execute SQL in database container
execute_sql() {
    local sql_file=$1
    print_status "Executing SQL: $sql_file"
    
    if [ -f "$sql_file" ]; then
        docker exec -i $DB_CONTAINER mysql -u onlyoffice_user -ponlyoffice_pass onlyoffice < "$sql_file"
        print_success "SQL executed successfully"
    else
        print_error "SQL file not found: $sql_file"
        return 1
    fi
}

# Function to copy files to container
copy_files_to_container() {
    local container=$1
    local dest_path="/var/www/onlyoffice/WebStudio"
    
    print_status "Copying CRM monitoring files to container..."
    
    # Copy the monitoring files
    docker cp "$SCRIPT_DIR/CrmEmailMonitoringJob.cs" "$container:$dest_path/"
    docker cp "$SCRIPT_DIR/CrmEmailMonitoringService.cs" "$container:$dest_path/"
    docker cp "$SCRIPT_DIR/CrmEmailMonitoringStartup.cs" "$container:$dest_path/"
    docker cp "$SCRIPT_DIR/CrmEmailMonitoringConfig.json" "$container:$dest_path/"
    
    print_success "Files copied to container"
}

# Function to add startup code to Global.asax.cs
patch_global_asax() {
    local container=$1
    
    print_status "Checking Global.asax.cs for CRM monitoring integration..."
    
    # Check if already integrated
    if docker exec $container grep -q "CrmEmailMonitoringInitializer" /var/www/onlyoffice/WebStudio/Global.asax.cs; then
        print_warning "CRM monitoring already integrated in Global.asax.cs"
        return 0
    fi
    
    print_status "Adding CRM monitoring to Global.asax.cs..."
    
    # Create a backup
    docker exec $container cp /var/www/onlyoffice/WebStudio/Global.asax.cs /var/www/onlyoffice/WebStudio/Global.asax.cs.backup
    
    # Add the integration code (this is a simplified approach)
    cat > /tmp/global_asax_patch.cs << 'EOF'
// Add this to your using statements
using ASC.Mail.Enhanced;

// Add this to Application_Start method
try
{
    log4net.LogManager.GetLogger(typeof(Global)).Info("🚀 Starting CRM Email Monitoring...");
    CrmEmailMonitoringInitializer.Initialize();
    log4net.LogManager.GetLogger(typeof(Global)).Info("✅ CRM Email Monitoring started successfully");
}
catch (Exception ex)
{
    log4net.LogManager.GetLogger(typeof(Global)).Error("❌ Error starting CRM Email Monitoring", ex);
}

// Add this to Application_End method  
try
{
    CrmEmailMonitoringInitializer.Shutdown();
}
catch (Exception ex)
{
    log4net.LogManager.GetLogger(typeof(Global)).Error("❌ Error stopping CRM Email Monitoring", ex);
}
EOF

    docker cp /tmp/global_asax_patch.cs "$container:/tmp/"
    
    print_warning "Manual integration required: Please add the contents of /tmp/global_asax_patch.cs to your Global.asax.cs file"
    print_warning "Backup created at: /var/www/onlyoffice/WebStudio/Global.asax.cs.backup"
}

# Function to setup test data
setup_test_data() {
    print_status "Setting up test data for CRM monitoring..."
    
    if [ -f "$SCRIPT_DIR/docker-test-setup.sql" ]; then
        execute_sql "$SCRIPT_DIR/docker-test-setup-fixed.sql"
        print_success "Test data setup completed"
    else
        print_error "Test data SQL file not found"
        return 1
    fi
}

# Function to test database connectivity
test_database() {
    print_status "Testing database connectivity..."
    
    # Test basic connection
    if docker exec $DB_CONTAINER mysql -u onlyoffice_user -ponlyoffice_pass -e "SELECT 1;" > /dev/null 2>&1; then
        print_success "Database connection successful"
    else
        print_error "Database connection failed"
        return 1
    fi
    
    # Check required tables exist
    local tables=("mail_mail" "crm_contact" "crm_contact_info" "crm_relationship_event" "mail_chain_x_crm_entity")
    
    for table in "${tables[@]}"; do
        if docker exec $DB_CONTAINER mysql -u onlyoffice_user -ponlyoffice_pass onlyoffice -e "DESCRIBE $table;" > /dev/null 2>&1; then
            print_success "Table $table exists"
        else
            print_error "Table $table not found"
            return 1
        fi
    done
}

# Function to check current email/CRM data
check_data_status() {
    print_status "Checking current email and CRM data..."
    
    # Count emails
    local email_count=$(docker exec $DB_CONTAINER mysql -u onlyoffice_user -ponlyoffice_pass onlyoffice -se "SELECT COUNT(*) FROM mail_mail;")
    print_status "Total emails in database: $email_count"
    
    # Count CRM contacts
    local contact_count=$(docker exec $DB_CONTAINER mysql -u onlyoffice_user -ponlyoffice_pass onlyoffice -se "SELECT COUNT(*) FROM crm_contact;")
    print_status "Total CRM contacts: $contact_count"
    
    # Count unprocessed emails
    local unprocessed=$(docker exec $DB_CONTAINER mysql -u onlyoffice_user -ponlyoffice_pass onlyoffice -se "
        SELECT COUNT(*) 
        FROM mail_mail m
        WHERE m.folder IN (1, 2)
          AND NOT EXISTS (
              SELECT 1 FROM crm_relationship_event cre 
              WHERE cre.entity_type = 0 AND cre.entity_id = m.id
          );")
    print_status "Unprocessed emails (ready for CRM linking): $unprocessed"
    
    if [ "$unprocessed" -gt 0 ]; then
        print_success "Found $unprocessed emails ready for CRM processing"
    else
        print_warning "No unprocessed emails found - consider running setup-test-data"
    fi
}

# Function to monitor logs
monitor_logs() {
    local container=$1
    print_status "Monitoring container logs for CRM processing..."
    print_status "Press Ctrl+C to stop monitoring"
    
    docker logs -f $container 2>&1 | grep -i "crm\|monitoring\|email" --color=always
}

# Function to restart application
restart_app() {
    local container=$1
    print_status "Restarting ONLYOFFICE application..."
    
    docker exec $container supervisorctl restart monoserve:onlyoffice
    sleep 5
    
    if docker exec $container supervisorctl status monoserve:onlyoffice | grep -q "RUNNING"; then
        print_success "Application restarted successfully"
    else
        print_error "Failed to restart application"
        return 1
    fi
}

# Main menu
show_menu() {
    echo
    echo "Choose an option:"
    echo "1. Check containers and database"
    echo "2. Setup test data"
    echo "3. Copy monitoring files to container"
    echo "4. Patch Global.asax.cs (manual steps)"
    echo "5. Check current data status"
    echo "6. Restart application"
    echo "7. Monitor logs"
    echo "8. Full installation (steps 1-6)"
    echo "9. Exit"
    echo
    read -p "Enter your choice [1-9]: " choice
}

# Process menu choice
process_choice() {
    case $choice in
        1)
            print_status "Checking containers and database..."
            if check_container $CONTAINER_NAME; then
                print_success "Container $CONTAINER_NAME is running"
            else
                print_error "Container $CONTAINER_NAME is not running"
                return 1
            fi
            
            if check_container $DB_CONTAINER; then
                print_success "Database container $DB_CONTAINER is running"
            else
                print_error "Database container $DB_CONTAINER is not running"
                return 1
            fi
            
            test_database
            ;;
        2)
            setup_test_data
            ;;
        3)
            copy_files_to_container $CONTAINER_NAME
            ;;
        4)
            patch_global_asax $CONTAINER_NAME
            ;;
        5)
            check_data_status
            ;;
        6)
            restart_app $CONTAINER_NAME
            ;;
        7)
            monitor_logs $CONTAINER_NAME
            ;;
        8)
            print_status "Starting full installation..."
            if check_container $CONTAINER_NAME && check_container $DB_CONTAINER; then
                test_database && \
                setup_test_data && \
                copy_files_to_container $CONTAINER_NAME && \
                patch_global_asax $CONTAINER_NAME && \
                check_data_status && \
                restart_app $CONTAINER_NAME
                
                if [ $? -eq 0 ]; then
                    print_success "Full installation completed!"
                    print_warning "Don't forget to manually integrate the Global.asax.cs changes"
                else
                    print_error "Installation failed at some step"
                fi
            else
                print_error "Required containers are not running"
            fi
            ;;
        9)
            print_success "Goodbye!"
            exit 0
            ;;
        *)
            print_error "Invalid choice"
            ;;
    esac
}

# Main execution
main() {
    # Check if Docker is available
    if ! command -v docker &> /dev/null; then
        print_error "Docker is not installed or not in PATH"
        exit 1
    fi
    
    # Check if running from correct directory
    if [ ! -f "$SCRIPT_DIR/CrmEmailMonitoringJob.cs" ]; then
        print_error "CRM monitoring files not found in current directory"
        print_error "Please run this script from the directory containing the CRM monitoring files"
        exit 1
    fi
    
    # Interactive menu
    while true; do
        show_menu
        process_choice
        echo
        read -p "Press Enter to continue..."
    done
}

# Run if executed directly
if [ "${BASH_SOURCE[0]}" == "${0}" ]; then
    main "$@"
fi
