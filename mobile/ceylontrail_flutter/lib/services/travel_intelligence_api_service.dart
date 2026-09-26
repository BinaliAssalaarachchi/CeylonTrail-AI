import 'package:dio/dio.dart';

import '../models/travel_intelligence_outcome_model.dart';
import '../models/agent_workflow_model.dart';
import 'api_client.dart';

abstract interface class TravelIntelligenceOutcomeSource {
  Future<TravelIntelligenceOutcome?> fetchLatest(String tripId);
}

abstract interface class AgentWorkflowSource {
  Future<AgentWorkflow?> fetchWorkflow(String tripId);
}

class TravelIntelligenceApiService implements TravelIntelligenceOutcomeSource, AgentWorkflowSource {
  const TravelIntelligenceApiService(this._client) : _request = null;

  const TravelIntelligenceApiService.forTesting(this._request)
      : _client = null;

  final ApiClient? _client;
  final Future<Response<dynamic>> Function(String path)? _request;

  @override
  Future<TravelIntelligenceOutcome?> fetchLatest(String tripId) async {
    try {
      final response = await (_request?.call('/api/trips/$tripId/travel-intelligence/latest') ??
          _client!.get('/api/trips/$tripId/travel-intelligence/latest'));
      final data = response.data;
      if (data is! Map<String, dynamic>) {
        throw const ApiException('Travel safety response was malformed.');
      }
      return TravelIntelligenceOutcome.fromJson(data);
    } on ApiException catch (error) {
      if (error.statusCode == 404) return null;
      rethrow;
    }
  }

  @override
  Future<AgentWorkflow?> fetchWorkflow(String tripId) async {
    try {
      final response = await (_request?.call('/api/trips/$tripId/agent-workflow/latest') ??
          _client!.get('/api/trips/$tripId/agent-workflow/latest'));
      final data = response.data;
      if (data is! Map<String, dynamic>) throw const ApiException('Workflow response was malformed.');
      return AgentWorkflow.fromJson(data);
    } on ApiException catch (error) {
      if (error.statusCode == 404) return null;
      rethrow;
    }
  }
}
