function EnvironmentController($scope, $http, umbRequestHelper) {
    let vm = this;
    let baseApiUrl = "backoffice/Api/Environment/";

    function init() {
        umbRequestHelper.resourcePromise(
            $http.get(baseApiUrl + "GetEnvironment")
        ).then(function(data) {
            vm.data = data;
        });
    }
    
    init();
}

angular.module("umbraco").controller("Cultiv.EnvironmentInspectController", EnvironmentController);