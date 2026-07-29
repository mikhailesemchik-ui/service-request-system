using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceRequest.Application.Categories;

namespace ServiceRequest.Api.Controllers;

[ApiController]
[Route("api/categories")]
public sealed class CategoriesController : ControllerBase
{
    private readonly IRequestCategoryService _categoryService;

    public CategoriesController(IRequestCategoryService categoryService)
    {
        _categoryService = categoryService;
    }

    [HttpGet]
    [Authorize]
    [ProducesResponseType(typeof(IReadOnlyList<CategoryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CategoryDto>>> GetAll(
        [FromQuery] bool includeInactive,
        CancellationToken cancellationToken)
    {
        var categories = await _categoryService.GetAllAsync(includeInactive, cancellationToken);
        return Ok(categories);
    }

    [HttpGet("{categoryId}")]
    [Authorize]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CategoryDto>> GetById(
        [Range(1, int.MaxValue, ErrorMessage = "Category id must be greater than zero.")] int categoryId,
        CancellationToken cancellationToken)
    {
        var category = await _categoryService.GetByIdAsync(categoryId, cancellationToken);
        return Ok(category);
    }

    [HttpPost]
    [Authorize(Policy = "RequireAdmin")]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CategoryDto>> Create(
        [FromBody] CreateCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var category = await _categoryService.CreateAsync(request.Name, request.Description, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { categoryId = category.Id }, category);
    }

    [HttpPut("{categoryId}")]
    [Authorize(Policy = "RequireAdmin")]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CategoryDto>> Update(
        [Range(1, int.MaxValue, ErrorMessage = "Category id must be greater than zero.")] int categoryId,
        [FromBody] UpdateCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var category = await _categoryService.UpdateAsync(categoryId, request.Name, request.Description, cancellationToken);
        return Ok(category);
    }

    [HttpPatch("{categoryId}/active-state")]
    [Authorize(Policy = "RequireAdmin")]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CategoryDto>> SetActiveState(
        [Range(1, int.MaxValue, ErrorMessage = "Category id must be greater than zero.")] int categoryId,
        [FromBody] UpdateCategoryActiveStateRequest request,
        CancellationToken cancellationToken)
    {
        var category = await _categoryService.SetActiveStateAsync(categoryId, request.IsActive!.Value, cancellationToken);
        return Ok(category);
    }
}
