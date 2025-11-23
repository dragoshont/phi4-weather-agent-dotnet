#!/bin/bash
# Start Aspire Host for Local AI Agent (Linux/macOS)
# Ensures prerequisites are running and launches the application

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
GRAY='\033[0;90m'
NC='\033[0m'

echo -e "${CYAN}========================================"
echo -e "  Starting Local AI Agent"
echo -e "========================================${NC}"
echo ""

# ============================================
# 1. Check Docker
# ============================================
echo -e "${YELLOW}[1/3] Verifying Docker...${NC}"

if ! command -v docker &> /dev/null; then
    echo -e "${RED}  ✗ Docker not installed${NC}"
    echo -e "${YELLOW}  Run setup script: ./scripts/setup-$(uname -s | tr '[:upper:]' '[:lower:]').sh${NC}"
    exit 1
fi

if ! docker ps &> /dev/null; then
    echo -e "${RED}  ✗ Docker daemon is not running${NC}"
    echo -e "${YELLOW}  Start Docker: sudo systemctl start docker${NC}"
    exit 1
fi

echo -e "${GREEN}  ✓ Docker is running${NC}"

# ============================================
# 2. Check AI Provider Service
# ============================================
echo ""
echo -e "${YELLOW}[2/3] Checking AI provider service...${NC}"

# Detect OS for provider check
OS=$(uname -s)

case "$OS" in
    Linux)
        # Linux uses Ollama
        if ! command -v ollama &> /dev/null; then
            echo -e "${RED}  ✗ Ollama not installed${NC}"
            echo -e "${YELLOW}  Run: ./scripts/setup-linux.sh${NC}"
            exit 1
        fi

        # Check if Ollama service is accessible
        if ! pgrep -x ollama > /dev/null; then
            echo -e "${YELLOW}  Starting Ollama service...${NC}"
            ollama list > /dev/null 2>&1 &
            sleep 2
        fi

        if curl -s http://localhost:11434/api/tags > /dev/null 2>&1; then
            echo -e "${GREEN}  ✓ Ollama service running on port 11434${NC}"
        else
            echo -e "${RED}  ✗ Ollama service not accessible${NC}"
            echo -e "${YELLOW}  Start: ollama serve (in another terminal)${NC}"
            exit 1
        fi
        ;;

    Darwin)
        # macOS can use Foundry Local or Ollama
        if command -v foundry &> /dev/null; then
            # Foundry Local available
            foundry service start > /dev/null 2>&1

            # Extract port from status (default 62859)
            FOUNDRY_STATUS=$(foundry service status 2>&1)
            if [[ $FOUNDRY_STATUS =~ http://[^:]+:([0-9]+) ]]; then
                FOUNDRY_PORT="${BASH_REMATCH[1]}"
                export FOUNDRY_PORT
                echo -e "${GREEN}  ✓ Foundry Local running on port $FOUNDRY_PORT${NC}"
            else
                export FOUNDRY_PORT="62859"
                echo -e "${GREEN}  ✓ Foundry Local started${NC}"
            fi
        elif command -v ollama &> /dev/null; then
            # Fall back to Ollama
            if ! pgrep -x ollama > /dev/null; then
                ollama list > /dev/null 2>&1 &
                sleep 2
            fi

            if curl -s http://localhost:11434/api/tags > /dev/null 2>&1; then
                echo -e "${GREEN}  ✓ Ollama service running on port 11434${NC}"
            else
                echo -e "${RED}  ✗ Ollama service not accessible${NC}"
                echo -e "${YELLOW}  Start: ollama serve${NC}"
                exit 1
            fi
        else
            echo -e "${RED}  ✗ No AI provider found (Foundry Local or Ollama)${NC}"
            echo -e "${YELLOW}  Run: ./scripts/setup-macos.sh${NC}"
            exit 1
        fi
        ;;

    *)
        echo -e "${RED}  ✗ Unsupported OS: $OS${NC}"
        exit 1
        ;;
esac

# ============================================
# 3. Validate Model Configuration
# ============================================
echo ""
echo -e "${YELLOW}[3/3] Validating model configuration...${NC}"

CONFIG_PATH="src/LocalAIAgent.Web/appsettings.json"
if [ ! -f "$CONFIG_PATH" ]; then
    CONFIG_PATH="appsettings.json"
fi

if [ ! -f "$CONFIG_PATH" ]; then
    echo -e "${RED}  ✗ Configuration file not found: $CONFIG_PATH${NC}"
    exit 1
fi

# Extract DefaultModel using jq (or grep fallback)
if command -v jq &> /dev/null; then
    DEFAULT_MODEL=$(jq -r '.AI.DefaultModel // empty' "$CONFIG_PATH")
    PROVIDER=$(jq -r ".AI.Models[] | select(.Name == \"$DEFAULT_MODEL\") | .Provider // empty" "$CONFIG_PATH")
    MODEL_ID=$(jq -r ".AI.Models[] | select(.Name == \"$DEFAULT_MODEL\") | .ModelId // empty" "$CONFIG_PATH")
else
    # Fallback to grep (less reliable but no dependency)
    DEFAULT_MODEL=$(grep -o '"DefaultModel": *"[^"]*"' "$CONFIG_PATH" | cut -d'"' -f4)
    # Cannot easily extract nested model details without jq, skip validation
    echo -e "${YELLOW}  ⚠ jq not installed - skipping detailed model validation${NC}"
    echo -e "${GRAY}  Install jq: sudo apt-get install jq (Linux) / brew install jq (macOS)${NC}"
    PROVIDER=""
    MODEL_ID=""
fi

if [ -z "$DEFAULT_MODEL" ]; then
    echo -e "${RED}  ✗ 'AI:DefaultModel' not set in $CONFIG_PATH${NC}"
    exit 1
fi

echo -e "${CYAN}  Default model: $DEFAULT_MODEL${NC}"

if [ -n "$PROVIDER" ] && [ -n "$MODEL_ID" ]; then
    echo -e "${CYAN}  Provider: $PROVIDER${NC}"
    echo -e "${CYAN}  Model ID: $MODEL_ID${NC}"

    # Validate model availability based on provider
    case "$PROVIDER" in
        Ollama)
            if command -v ollama &> /dev/null; then
                if ollama list 2>/dev/null | grep -q "$MODEL_ID"; then
                    echo -e "${GREEN}  ✓ Model available in Ollama${NC}"
                else
                    echo -e "${RED}  ✗ Model not available in Ollama${NC}"
                    echo -e "${YELLOW}  Download: ollama pull $MODEL_ID${NC}"
                    echo ""
                    echo -e "${YELLOW}  Available Ollama models:${NC}"
                    ollama list 2>/dev/null | tail -n +2 | awk '{print "    - " $1}'
                    echo ""
                    exit 1
                fi
            else
                echo -e "${RED}  ✗ Ollama not installed (required for provider 'Ollama')${NC}"
                exit 1
            fi
            ;;

        FoundryLocal)
            if command -v foundry &> /dev/null; then
                if foundry cache list 2>/dev/null | grep -q "$MODEL_ID"; then
                    echo -e "${GREEN}  ✓ Model available in Foundry cache${NC}"
                else
                    echo -e "${RED}  ✗ Model not available in Foundry cache${NC}"
                    echo -e "${YELLOW}  Download: foundry model download $MODEL_ID${NC}"
                    echo ""
                    exit 1
                fi
            else
                echo -e "${RED}  ✗ Foundry Local not installed (required for provider 'FoundryLocal')${NC}"
                exit 1
            fi
            ;;

        AzureOpenAI|OpenAI|Gemini|FoundryCloud)
            # Cloud providers - cannot validate locally, assume configured
            echo -e "${GREEN}  ✓ Cloud provider configured (cannot validate locally)${NC}"
            ;;

        *)
            echo -e "${YELLOW}  ⚠ Unknown provider '$PROVIDER' - skipping validation${NC}"
            ;;
    esac
fi

# ============================================
# 4. Launch Aspire AppHost
# ============================================
echo ""
echo -e "${CYAN}========================================"
echo -e "  Launching Aspire AppHost"
echo -e "========================================${NC}"
echo ""

# Navigate to repo root if in scripts directory
if [ -d "../src/LocalAIAgent.AppHost" ]; then
    cd ..
fi

# Launch AppHost
echo -e "${GRAY}Starting .NET Aspire host...${NC}"
echo ""

if [ -f "src/LocalAIAgent.AppHost/LocalAIAgent.AppHost.csproj" ]; then
    dotnet run --project src/LocalAIAgent.AppHost/LocalAIAgent.AppHost.csproj
else
    echo -e "${RED}  ✗ AppHost project not found${NC}"
    exit 1
fi
