using SABPlus.Radial.Core.Models;
using SABPlus.Radial.Core.Services;
using System;
using System.Linq;

namespace SABPlus.Radial.Overlay.Services
{
    public sealed class ProjectWheelStateService
    {
        private readonly WheelSettingsRepository _repository;
        private readonly object _sync = new object();
        private ProjectWheelStateCollection _states;

        public ProjectWheelStateService(WheelSettingsRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _states = _repository.LoadProjectStates();
        }

        public string GetActiveProfileId(string projectKey)
        {
            if (string.IsNullOrWhiteSpace(projectKey))
            {
                return string.Empty;
            }

            lock (_sync)
            {
                ProjectWheelState state = _states.Projects.FirstOrDefault(
                    item => string.Equals(item.ProjectKey, projectKey, StringComparison.OrdinalIgnoreCase));
                return state?.ActiveProfileId ?? string.Empty;
            }
        }

        public void SetActiveProfileId(string projectKey, string profileId)
        {
            if (string.IsNullOrWhiteSpace(projectKey))
            {
                return;
            }

            lock (_sync)
            {
                ProjectWheelState state = _states.Projects.FirstOrDefault(
                    item => string.Equals(item.ProjectKey, projectKey, StringComparison.OrdinalIgnoreCase));

                if (state == null)
                {
                    state = new ProjectWheelState { ProjectKey = projectKey };
                    _states.Projects.Add(state);
                }

                state.ActiveProfileId = profileId ?? string.Empty;
                state.LastUpdatedUtc = DateTime.UtcNow;
                _repository.SaveProjectStates(_states);
            }
        }
    }
}
