function EnvironmentController($scope, $http, umbRequestHelper, notificationsService) {
    let vm = this;
    let baseApiUrl = "backoffice/Api/Environment/";

    // State variables
    vm.data = [];
    vm.azureWebAppAdvancedCopy = false;
    vm.excludeEmptyValues = true;
    vm.onlyRedacted = false;
    vm.replaceColonWithUnderscore = false;
    vm.isLoading = true;

    // Copy to clipboard functionality
    vm.copyToClipboard = function(text) {
        navigator.clipboard.writeText(text)
            .then(function() {
                notificationsService.success("Copied!", "Value copied to clipboard");
            })
            .catch(function(err) {
                console.error('Failed to copy text: ', err);
                notificationsService.error("Copy Failed", "Could not copy to clipboard");
            });
    };

    // Format key based on toggle
    vm.formatKey = function(key) {
        if (vm.replaceColonWithUnderscore) {
            return key.replace(/:/g, '__');
        }
        return key;
    };

    // Generate Azure Web App JSON snippet
    vm.generateAzureSnippet = function(key, value) {
        var formattedKey = vm.formatKey(key);
        return JSON.stringify({
            name: formattedKey,
            value: value,
            slotSetting: false
        }, null, 2);
    };

    // Get redaction emoji
    vm.getRedactionEmoji = function(redactedMode) {
        switch (redactedMode) {
            case 'Full':
                return '🔒'; // Full redaction - locked
            case 'Partial':
                return '👁️'; // Partial redaction - eye
            case 'Advanced':
                return '🔐'; // Advanced redaction - locked with key
            default:
                return ''; // No redaction
        }
    };

    // Check if there are any redacted values
    vm.hasAnyRedactions = function() {
        return vm.data.some(function(item) {
            return item.RedactedMode != null && item.RedactedMode !== '';
        });
    };

    // Filter function for ng-repeat
    vm.filterVariables = function(item) {
        // Filter out empty values if enabled
        if (vm.excludeEmptyValues && (item.Value == null || item.Value === '')) {
            return false;
        }

        // Filter to only redacted items if enabled
        if (vm.onlyRedacted && (item.RedactedMode == null || item.RedactedMode === '')) {
            return false;
        }

        return true;
    };

    function init() {
        umbRequestHelper.resourcePromise(
            $http.get(baseApiUrl + "GetEnvironment")
        ).then(function(response) {
            vm.data = response.Variables;
            vm.azureWebAppAdvancedCopy = response.AzureWebAppAdvancedCopy;
            vm.isLoading = false;
        });
    }

    init();
}

angular.module("umbraco").controller("Cultiv.EnvironmentInspectController", EnvironmentController);