#!/usr/bin/env bash

set -euo pipefail

emit_allow() {
  printf '%s\n' '{"continue":true}'
}

emit_deny() {
  local target="$1"
  local pattern="$2"

  jq -cn \
    --arg target "$target" \
    --arg pattern "$pattern" \
    '{
      hookSpecificOutput: {
        hookEventName: "PreToolUse",
        permissionDecision: "deny",
        permissionDecisionReason: ("Blocked by .copilotignore: '\''" + $target + "'\'' matches exclusion '\''" + $pattern + "'\''.")
      },
      systemMessage: "Access to excluded files is denied by repository policy."
    }'
}

trim() {
  local value="$1"
  value="${value#${value%%[![:space:]]*}}"
  value="${value%${value##*[![:space:]]}}"
  printf '%s' "$value"
}

strip_quotes() {
  local value="$1"
  if [[ ${#value} -ge 2 ]]; then
    local first="${value:0:1}"
    local last="${value: -1}"
    if [[ ( "$first" == '"' || "$first" == "'" ) && "$first" == "$last" ]]; then
      value="${value:1:${#value}-2}"
    fi
  fi
  printf '%s' "$value"
}

normalize_path() {
  local candidate="$1"
  local cwd="$2"

  candidate="${candidate//\\//}"
  cwd="${cwd//\\//}"

  if [[ "$candidate" == "$cwd"/* ]]; then
    candidate="${candidate#"$cwd"/}"
  fi

  while [[ "$candidate" == /* ]]; do
    candidate="${candidate#/}"
  done

  printf '%s' "$candidate"
}

matches_pattern() {
  local candidate="$1"
  local cwd="$2"
  local pattern="$3"
  local normalized relative rooted basename

  normalized="${candidate//\\//}"
  relative="$(normalize_path "$candidate" "$cwd")"
  rooted="/$relative"
  basename="${relative##*/}"

  [[ -n "$normalized" && "$normalized" == $pattern ]] && return 0
  [[ -n "$relative" && "$relative" == $pattern ]] && return 0
  [[ -n "$rooted" && "$rooted" == $pattern ]] && return 0
  [[ -n "$basename" && "$basename" == $pattern ]] && return 0

  return 1
}

load_patterns() {
  local ignore_file="$1/.copilotignore"
  local line trimmed

  if [[ ! -f "$ignore_file" ]]; then
    return 0
  fi

  while IFS= read -r line || [[ -n "$line" ]]; do
    trimmed="$(trim "$line")"
    [[ -z "$trimmed" || "$trimmed" == \#* ]] && continue
    if [[ "$trimmed" == -* ]]; then
      trimmed="$(trim "${trimmed#-}")"
    fi
    trimmed="$(strip_quotes "$trimmed")"
    [[ -n "$trimmed" ]] && printf '%s\n' "$trimmed"
  done < "$ignore_file"
}

check_candidate_against_patterns() {
  local candidate="$1"
  local cwd="$2"
  shift 2
  local pattern

  for pattern in "$@"; do
    if matches_pattern "$candidate" "$cwd" "$pattern"; then
      printf '%s' "$pattern"
      return 0
    fi
  done

  return 1
}

extract_paths_for_tool() {
  local tool_name="$1"
  local event_json="$2"

  case "$tool_name" in
    create_file)
      jq -r '.tool_input.filePath? // empty' <<< "$event_json"
      ;;
    read_file)
      jq -r '.tool_input.filePath? // empty' <<< "$event_json"
      ;;
    edit_file)
      jq -r '.tool_input.filePath? // empty' <<< "$event_json"
      ;;
    insert_edit_into_file)
      jq -r '.tool_input.filePath? // empty' <<< "$event_json"
      ;;
    edit_notebook_file)
      jq -r '.tool_input.filePath? // empty' <<< "$event_json"
      ;;
    list_dir)
      jq -r '.tool_input.path? // empty' <<< "$event_json"
      ;;
    create_directory)
      jq -r '.tool_input.dirPath? // empty' <<< "$event_json"
      ;;
    get_errors)
      jq -r '.tool_input.filePaths[]? // empty' <<< "$event_json"
      ;;
    apply_patch)
      jq -r '.tool_input.input? // empty' <<< "$event_json" \
        | grep -E '^\*\*\* (Add|Update|Delete) File: ' \
        | sed -E 's/^\*\*\* (Add|Update|Delete) File: //' \
        | sed -E 's/ -> .*$//' \
        | sed 's/[[:space:]]*$//'
      ;;
  esac
}

check_terminal_command_for_excluded_paths() {
  local tool_name="$1"
  local event_json="$2"
  local cwd="$3"
  shift 3
  local command_str

  case "$tool_name" in
    run_in_terminal|run_command)
      command_str="$(jq -r '.tool_input.command? // empty' <<< "$event_json")"
      ;;
    *)
      return 1
      ;;
  esac

  [[ -z "$command_str" ]] && return 1

  # Replace shell operators with spaces for simple tokenization
  local sanitized="$command_str"
  sanitized="${sanitized//&&/ }"
  sanitized="${sanitized//||/ }"
  sanitized="${sanitized//|/ }"
  sanitized="${sanitized//;/ }"
  sanitized="${sanitized//>/ }"
  sanitized="${sanitized//</ }"

  local -a tokens
  read -ra tokens <<< "$sanitized"

  local token candidate matched_pattern

  for token in "${tokens[@]}"; do
    # Strip surrounding quotes from token
    token="$(strip_quotes "$token")"

    # Handle git show <ref>:<path> — extract path after colon
    if [[ "$token" == *:* ]]; then
      candidate="${token##*:}"
      if [[ -n "$candidate" ]]; then
        candidate="$(strip_quotes "$candidate")"
        if matched_pattern="$(check_candidate_against_patterns "$candidate" "$cwd" "$@")"; then
          printf '%s\t%s\n' "$candidate" "$matched_pattern"
          return 0
        fi
      fi
    fi

    # Check the token itself as a potential path
    if [[ -n "$token" ]] && matched_pattern="$(check_candidate_against_patterns "$token" "$cwd" "$@")"; then
      printf '%s\t%s\n' "$token" "$matched_pattern"
      return 0
    fi
  done

  return 1
}

search_targets_excluded_content() {
  local tool_name="$1"
  local event_json="$2"
  local cwd="$3"
  shift 3
  local value matched_pattern

  case "$tool_name" in
    file_search)
      value="$(jq -r '.tool_input.query? // empty' <<< "$event_json")"
      ;;
    grep_search)
      value="$(jq -r '.tool_input.includePattern? // empty' <<< "$event_json")"
      ;;
    *)
      return 1
      ;;
  esac

  [[ -z "$value" ]] && return 1

  if matched_pattern="$(check_candidate_against_patterns "$value" "$cwd" "$@")"; then
    printf '%s\t%s\n' "$value" "$matched_pattern"
    return 0
  fi

  return 1
}

main() {
  local event_json cwd tool_name search_match search_value search_pattern matched_pattern path pattern terminal_match terminal_value terminal_pattern
  local -a patterns

  event_json="$(cat)"
  if [[ -z "${event_json//[[:space:]]/}" ]]; then
    emit_allow
    return 0
  fi

  cwd="$(jq -r '.cwd // empty' <<< "$event_json")"
  if [[ -z "$cwd" ]]; then
    cwd="$PWD"
  fi

  tool_name="$(jq -r '.tool_name // empty' <<< "$event_json")"

  patterns=()
  while IFS= read -r pattern || [[ -n "$pattern" ]]; do
    [[ -n "$pattern" ]] && patterns+=("$pattern")
  done < <(load_patterns "$cwd")

  if [[ ${#patterns[@]} -eq 0 ]]; then
    emit_allow
    return 0
  fi

  if search_match="$(search_targets_excluded_content "$tool_name" "$event_json" "$cwd" "${patterns[@]}")"; then
    IFS=$'\t' read -r search_value search_pattern <<< "$search_match"
    emit_deny "$search_value" "$search_pattern"
    return 0
  fi

  # Check terminal/shell commands for excluded paths
  if terminal_match="$(check_terminal_command_for_excluded_paths "$tool_name" "$event_json" "$cwd" "${patterns[@]}")"; then
    IFS=$'\t' read -r terminal_value terminal_pattern <<< "$terminal_match"
    emit_deny "$terminal_value" "$terminal_pattern"
    return 0
  fi

  while IFS= read -r path || [[ -n "$path" ]]; do
    [[ -z "$path" ]] && continue
    if matched_pattern="$(check_candidate_against_patterns "$path" "$cwd" "${patterns[@]}")"; then
      emit_deny "$path" "$matched_pattern"
      return 0
    fi
  done < <(extract_paths_for_tool "$tool_name" "$event_json")

  emit_allow
}

main "$@"